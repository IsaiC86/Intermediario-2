using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace intermediarioDB.Pages
{
    public class TransaccionFila
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public string Titulo { get; set; } = "";
        public string? Imagen { get; set; }
        public string Contraparte { get; set; } = "";
        public decimal MontoProducto { get; set; }
        public decimal Comision { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = "";
        public string? Rastreo { get; set; }
        public DateTime? Fecha { get; set; }

        public string EstadoTexto => Estado switch
        {
            "PagoRetenidoEnGarantia" => "bg-warning text-dark",
            "Eviado" => "bg-ingo text-dark",
            "RecibidoConforme" => "bg-success",
            "EnDisputa" => "bg-danger",
            _ => "bg-secondary",
        };
    }

    public static class TransaccionesDb
    {
        // comoComprador = true  -> compras del usuario (contraparte = vendedor)
        // comoComprador = false -> ventas del usuario (contraparte = comprador)
        public static async Task<List<TransaccionFila>> ListarAsync(
            MySqlConnection conn, int usuarioId, bool comoComprador)
        {
            var columnaYo = comoComprador ? "comprador_id" : "vendedor_id";
            var columnaOtro = comoComprador ? "vendedor_id" : "comprador_id";

            var sql =
                "SELECT t.id, t.producto_id, p.titulo, p.imagen, u.nombre_completo AS contraparte, " +
                "t.monto_producto, t.comision_intermediario, t.monto_total, " +
                "t.estado_transaccion, t.codigo_rastreo_envio, t.fecha_creacion " +
                "FROM transacciones t " +
                "JOIN productos p ON p.id = t.producto_id " +
                $"JOIN usuarios u ON u.id = t.{columnaOtro} " +
                $"WHERE t.{columnaYo} = @yo ORDER BY t.id DESC";

            var lista = new List<TransaccionFila>();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@yo", usuarioId);
            using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                lista.Add(new TransaccionFila
                {
                    Id = r.GetInt32(r.GetOrdinal("id")),
                    ProductoId = r.GetInt32(r.GetOrdinal("producto_id")),
                    Titulo = r.GetString(r.GetOrdinal("titulo")),
                    Imagen = r.IsDBNull(r.GetOrdinal("imagen")) ? null : r.GetString(r.GetOrdinal("imagen")),
                    Contraparte = r.GetString(r.GetOrdinal("contraparte")),
                    MontoProducto = r.GetDecimal(r.GetOrdinal("monto_producto")),
                    Comision = r.GetDecimal(r.GetOrdinal("comision_intermediario")),
                    Total = r.GetDecimal(r.GetOrdinal("monto_total")),
                    Estado = r.IsDBNull(r.GetOrdinal("estado_transaccion")) ? "" : r.GetString(r.GetOrdinal("estado_transaccion")),
                    Rastreo = r.IsDBNull(r.GetOrdinal("codigo_rastreo_envio"))
                    ? null : r.GetString(r.GetOrdinal("codigo_rastreo_envio")),
                    Fecha = r.IsDBNull(r.GetOrdinal("fecha_creacion"))
                    ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("fecha_creacion"))
                });
            }
            return lista;
        }
    }

    [Authorize]
    public class MisComprasModel : PageModel
    {
        private readonly IConfiguration _config;
        public MisComprasModel(IConfiguration config) => _config = config;

        public List<TransaccionFila> Transacciones { get; set; } = new();

        private int UsuarioId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public async Task OngetAsync()
        {
            using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();
            Transacciones = await TransaccionesDb.ListarAsync(conn, UsuarioId, comoComprador: true);
        }


        // El comprador confirma que recibio el producto y se libera el pago al vendedor

        public async Task<IActionResult> OnPostRecibidoAsync(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                using var tx = await conn.BeginTransactionAsync();

                int filas;
                using (var cmd = new MySqlCommand(
                    "UPDATE transacciones SET estado_transaccion = 'FondosLiberados' " +
                    "WHERE id = @id AND comprador_id = @yo AND estado_transaccion = 'Enviado'", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@yo", UsuarioId);
                    filas = await cmd.ExecuteNonQueryAsync();
                }

                if (filas == 1)
                {
                    using var cmd2 = new MySqlCommand(
                        "UPDATE productos SET estado_producto = 'Vendido' " +
                        "WHERE id = (SELECT producto_id FROM transacciones WHERE id = @id)", conn, tx);
                    cmd2.Parameters.AddWithValue("@id", id);
                    await cmd2.ExecuteNonQueryAsync();
                    await tx.CommitAsync();

                    TempData["Mensaje"] = "Gracias por confirmar. Los fondos se liberaron al vendedor.";
                }
                else
                {
                    TempData["Error"] = "No se pudo confirmar: el pedido no está en estado Enviado.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
            }
            return RedirectToPage();
        }

        // El comprador inicia una disputa sobre la transacción
        // (cancela antes que el vendedor envie)

        public async Task<IActionResult> OnPostDisputaAsync(int id)
        {
            try
            {
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                using var tx = await conn.BeginTransactionAsync();

                int filas;
                using (var cmd = new MySqlCommand(
                    "UPDATE transacciones SET estado_transaccion = 'Cancelado' " +
                    "WHERE id = @id AND comprador_id = @yo AND estado_transaccion = 'PagoRetenidoEnGarantia'", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@yo", UsuarioId);
                    filas = await cmd.ExecuteNonQueryAsync();
                }

                if (filas == 1)
                {
                    using var cmd2 = new MySqlCommand(
                        "UPDATE productos SET estado_producto = 'Disponible' " +
                        "WHERE id = (SELECT producto_id FROM transacciones WHERE id = @id)", conn, tx);
                    cmd2.Parameters.AddWithValue("@id", id);
                    await cmd2.ExecuteNonQueryAsync();
                    await tx.CommitAsync();
                    TempData["Mensaje"] = "Compra cancelada. Tu dinero se devuelve y el producto vuelve a estar disponible.";
                }
                else
                {
                    TempData["Error"] = "No se puede cancelar: el vendedor ya envió el producto o el pedido ya cambió de estado.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
            }
            return RedirectToPage();
        }
    }
}


