using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace intermediarioDB.Pages
{
    public class Producto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public decimal Precio { get; set; }
        public string? Imagen { get; set; }

    }
    public class ProductosModel : PageModel
    {
        private readonly IConfiguration _config;
        public ProductosModel(IConfiguration config) => _config = config;

        public List<Producto> Productos { get; set; } = new();
        public string Error { get; set; } = "";

        public async Task OnGetAsync()
        {
            try
            {
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                using var cmd = new MySqlCommand(
                    "SELECT id, titulo, descripcion, precio, imagen FROM productos " +
                    "WHERE estado_producto = 'Disponible' ORDER BY fecha_publicacion DESC", conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    Productos.Add(new Producto
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        Titulo = reader.GetString(reader.GetOrdinal("titulo")),
                        Descripcion = reader.IsDBNull(reader.GetOrdinal("descripcion")) ? "" : reader.GetString(reader.GetOrdinal("descripcion")),
                        Precio = reader.GetDecimal(reader.GetOrdinal("precio")),
                        Imagen = reader.IsDBNull(reader.GetOrdinal("imagen")) ? null : reader.GetString(reader.GetOrdinal("imagen"))
                    });
                }
            }
            catch (Exception ex)
            {
                Error = "No se pudieron cargar los productos: " + ex.Message;

            }
        }
    }
}






