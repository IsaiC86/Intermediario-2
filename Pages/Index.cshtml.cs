using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace intermediarioDB.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _config;
        public string Mensaje { get; set; } = "";
        public IndexModel(IConfiguration config) => _config = config;
        public async Task OnGetAsync()
        {
            try
            {
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                using var cmd = new MySqlCommand("SELECT COUNT(*) FROM usuarios", conn);
                var total = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                Mensaje = $"Conexion exitosa. Usuarios en la pagina: {total}";
            }
            catch (Exception ex)
            {
                Mensaje = $"Error al conectar a la base de datos: {ex.Message}";
            }



        }
    }
}
