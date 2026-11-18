using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace intermediarioDB.Pages
{
    public class ProductoDetalle
    {
        public int Id { get; set; }
        public int VendedorId { get; set; }
        public string Titulo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public decimal Precio { get; set; }
        public string? Imagen { get; set; }
        public string Estado { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string Vendedor { get; set; } = "";
        public string VendedorVerificado { get; set; } = "";
    }

    public class DetalleModel : PageModel
    {
        public const decimal PorcentajeComision = 0.10m;

        private readonly IConfiguration _config;
        public DetalleModel(IConfiguration config) => _config = config;

        public ProductoDetalle? Producto { get; set; }

        public decimal Comision => Producto == null ? 0 : Math.Round(Producto.Precio * PorcentajeComision, 2);
        public decimal Total => Producto == null ? 0 : Producto.Precio + Comision;

        public int? UsuarioActualId
        {
            get
            {
                var v = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return int.TryParse(v, out var id) ? id : null;
            }
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await CargarProductoAsync(id)) return NotFound();
            return Page();
        }

        public async Task<IActionResult> OnPostComprarAsync(int id)
        {
            // 1. Debe iniciar sesión
            if (UsuarioActualId == null)
                return RedirectToPage("/Login", new { returnUrl = Url.Page("/Detalle", new { id }) });

            var compradorId = UsuarioActualId.Value;

            try
            {
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                using var tx = await conn.BeginTransactionAsync();

                // 2. Leer el producto y bloquearlo mientras dura la compra
                int vendedorId;
                decimal precio;
                string estado;
                using (var cmd = new MySqlCommand(
                    "SELECT vendedor_id, precio, estado_producto FROM productos WHERE id = @id FOR UPDATE",
                    conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync()) return NotFound();
                    vendedorId = reader.GetInt32(0);
                    precio = reader.GetDecimal(1);
                    estado = reader.GetString(2);
                }

                // 3. Validaciones
                if (vendedorId == compradorId)
                {
                    TempData["Error"] = "No puedes comprar tu propio producto.";
                    return RedirectToPage("/Detalle", new { id });
                }
                if (estado != "Disponible")
                {
                    TempData["Error"] = "Este producto ya no está disponible.";
                    return RedirectToPage("/Detalle", new { id });
                }

                // 4. Calcular comisión (10%) y total, que paga el comprador
                var comision = Math.Round(precio * PorcentajeComision, 2);
                var total = precio + comision;

                // 5. Marcar el producto como en transacción
                using (var cmd = new MySqlCommand(
                    "UPDATE productos SET estado_producto = 'EnTransaccion' " +
                    "WHERE id = @id AND estado_producto = 'Disponible'", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    await cmd.ExecuteNonQueryAsync();
                }

                // 6. Crear la transacción con el pago retenido en garantía
                using (var cmd = new MySqlCommand(
                    "INSERT INTO transacciones (producto_id, comprador_id, vendedor_id, monto_producto, " +
                    "comision_intermediario, monto_total, estado_transaccion) " +
                    "VALUES (@producto, @comprador, @vendedor, @monto, @comision, @total, 'PagoRetenidoEnGarantia')",
                    conn, tx))
                {
                    cmd.Parameters.AddWithValue("@producto", id);
                    cmd.Parameters.AddWithValue("@comprador", compradorId);
                    cmd.Parameters.AddWithValue("@vendedor", vendedorId);
                    cmd.Parameters.AddWithValue("@monto", precio);
                    cmd.Parameters.AddWithValue("@comision", comision);
                    cmd.Parameters.AddWithValue("@total", total);
                    await cmd.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();

                TempData["Mensaje"] =
                    $"Compra registrada. Pagaste ${total:N2} MXN (incluye ${comision:N2} de comisión). " +
                    "Tu dinero queda retenido en garantía hasta que confirmes que recibiste el producto.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "No se pudo completar la compra: " + ex.Message;
            }

            return RedirectToPage("/Detalle", new { id });
        }

        private async Task<bool> CargarProductoAsync(int id)
        {
            using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            using var cmd = new MySqlCommand(
                "SELECT p.id, p.vendedor_id, p.titulo, p.descripcion, p.precio, p.imagen, p.estado_producto, " +
                "p.fecha_publicacion, u.nombre_completo, u.estado_verificacion " +
                "FROM productos p JOIN usuarios u ON u.id = p.vendedor_id " +
                "WHERE p.id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return false;

            Producto = new ProductoDetalle
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                VendedorId = reader.GetInt32(reader.GetOrdinal("vendedor_id")),
                Titulo = reader.GetString(reader.GetOrdinal("titulo")),
                Descripcion = reader.IsDBNull(reader.GetOrdinal("descripcion"))
                    ? "" : reader.GetString(reader.GetOrdinal("descripcion")),
                Precio = reader.GetDecimal(reader.GetOrdinal("precio")),
                Imagen = reader.IsDBNull(reader.GetOrdinal("imagen"))
                    ? null : reader.GetString(reader.GetOrdinal("imagen")),
                Estado = reader.GetString(reader.GetOrdinal("estado_producto")),
                Fecha = reader.GetDateTime(reader.GetOrdinal("fecha_publicacion")),
                Vendedor = reader.GetString(reader.GetOrdinal("nombre_completo")),
                VendedorVerificado = reader.GetString(reader.GetOrdinal("estado_verificacion"))
            };
            return true;
        }
    }
}