using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace intermediarioDB.Pages
{
    [Authorize]
    public class MisVentasModel : PageModel
    {
        private readonly IConfiguration _config;
        public MisVentasModel(IConfiguration config) => _config = config;

        public List<TransaccionFila> Transacciones { get; set; } = new();

        private int UsuarioId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public async Task OnGetAsync()
        {
            using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();
            Transacciones = await TransaccionesDb.ListarAsync(conn, UsuarioId, comoComprador: false);
        }

        // El vendedor captura el código de rastreo y marca el pedido como enviado
        public async Task<IActionResult> OnPostEnviarAsync(int id, string? codigo)
        {
            codigo = codigo?.Trim();
            if (string.IsNullOrEmpty(codigo) || codigo.Length > 100)
            {
                TempData["Error"] = "Escribe un código de rastreo válido (máximo 100 caracteres).";
                return RedirectToPage();
            }
            try
            {
                using var conn = new MySqlConnection(_config.GetConnectionString("Default Connection"));
                    await conn.OpenAsync();

                using var cmd = new MySqlCommand(
                    "UPDATE transacciones SET estado_transaccion = 'Enviado', codigo_rastreo_envio = @codigo " +
                    "WHERE id = @id AND vendedor_id = @yo AND esrado_transaccion = 'PagoRetendiEnGarantia'", conn);
                cmd.Parameters.AddWithValue("@codigo", codigo);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@yo", UsuarioId);

                var filas = await cmd.ExecuteNonQueryAsync();

                TempData[filas == 1 ? "Mensaje" : "Error"] = filas == 1
                    ? "Pedido marcado como enviado. El comprador ya puede ver tu código de rastro"
                : "No se pudo actualizar: el pedido ya no esta pendiente de envío";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
            }
            return RedirectToPage();
        }
    }
}


