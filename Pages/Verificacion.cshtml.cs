using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace intermediarioDB.Pages
{
    public class UsuarioFila
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string Correo { get; set; } = "";
        public string? Telefono { get; set; }
        public string Estado { get; set; } = "";
    }

    // Consultas que comparten varias paginas

    public static class UsuariosDb
    {
        public static async Task<bool> EsAdminAsync(MySqlConnection conn, int usuarioId)
        {
            using var cmd = new MySqlCommand("SELECT es_admin FROM usuarios WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", usuarioId);
            var r = await cmd.ExecuteScalarAsync();
            return r != null && Convert.ToInt32(r) == 1;
        }

        public static async Task<string> EstadoAsync(MySqlConnection conn, int usuarioId)
        {
            using var cmd = new MySqlCommand("SELECT estado_verificacion FROM usuarios WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", usuarioId);
            var r = await cmd.ExecuteScalarAsync();
            return r?.ToString() ?? "";
        }
    }

    [Authorize]
    public class VerificacionModel : PageModel
    {
        private readonly IConfiguration _config;
        public VerificacionModel(IConfiguration config) => _config = config;

        public List<UsuarioFila> Usuarios { get; set; } = new();

        private int UsuarioId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public async Task<IActionResult> OnGetAsync()
        {
            using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            if (!await UsuariosDb.EsAdminAsync(conn, UsuarioId))
                return RedirectToPage("/Index");

            using var cmd = new MySqlCommand(
                "SELECT id, nombre_completo, correo, telefono, estado_verificacion FROM usuarios " +
                "ORDER BY FIELD(estado_verificacion, 'Pendiente', 'Rechazado', 'Verificado'), id DESC", conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                Usuarios.Add(new UsuarioFila
                {
                    Id = r.GetInt32(r.GetOrdinal("id")),
                    Nombre = r.GetString(r.GetOrdinal("nombre_completo")),
                    Correo = r.GetString(r.GetOrdinal("correo")),
                    Telefono = r.IsDBNull(r.GetOrdinal("telefono")) ? null : r.GetString(r.GetOrdinal("telefono")),
                    Estado = r.GetString(r.GetOrdinal("estado_verificacion"))
                });
            }
            return Page();
        }

        public async Task<IActionResult> OnPostCambiarAsync(int id, string estado)
        {
            if (estado != "Verificado" && estado != "Rechazado" && estado != "Pendiente")
                return RedirectToPage();

            try
            {
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                if (!await UsuariosDb.EsAdminAsync(conn, UsuarioId))
                    return RedirectToPage("/Index");

                using var cmd = new MySqlCommand(
                    "UPDATE usuarios SET estado_verificacion = @estado WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("@estado", estado);
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();

                TempData["Mensaje"] = $"Usuario actualizado a: {estado}. ";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
            }
            return RedirectToPage();

        }
    }
}
