using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace intermediarioDB.Pages
{
    [Authorize]
    public class PublicarModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public PublicarModel(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
        }

        [BindProperty]
        [Required(ErrorMessage = "Escribe el título")]
        [StringLength(150)]
        [Display(Name = "Título")]
        public string Titulo { get; set; } = "";

        [BindProperty]
        [Display(Name = "Descripción")]
        public string? Descripcion { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Escribe el precio")]
        [Range(0.01, 99999999, ErrorMessage = "Precio no válido")]
        [Display(Name = "Precio (MXN)")]
        public decimal Precio { get; set; }

        [BindProperty]
        [Display(Name = "Foto")]
        public IFormFile? Foto { get; set; }

        public string Mensaje { get; set; } = "";
        public bool Verificado { get; set; }
        public string EstadoVerificacion { get; set; } = "";

        private static readonly string[] Extensiones = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxBytes = 3 * 1024 * 1024; // 3 MB

        public async Task OnGetAsync()
        {
            var yo = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();
            EstadoVerificacion = await UsuariosDb.EstadoAsync(conn, yo);
            Verificado = EstadoVerificacion == "Verificado";
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var yo = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            using (var connCheck = new MySqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await connCheck.OpenAsync();
                EstadoVerificacion = await UsuariosDb.EstadoAsync(connCheck, yo);
                Verificado = EstadoVerificacion == "Verificado";
            }
            if (!Verificado) return Page();

            if (Foto == null || Foto.Length == 0)
                ModelState.AddModelError("Foto", "Selecciona una foto");
            else
            {
                var ext = Path.GetExtension(Foto.FileName).ToLowerInvariant();
                if (!Extensiones.Contains(ext))
                    ModelState.AddModelError(nameof(Foto), "Solo se permiten imagenes .jpg, .jpeg, .png y .webp");
                else if (Foto.Length > MaxBytes)
                    ModelState.AddModelError(nameof(Foto), "La imagen no debe pesar mas de 3 MB");
            }

            if (!ModelState.IsValid) return Page();

            try
            {
                var extension = Path.GetExtension(Foto!.FileName).ToLowerInvariant();
                var nombreArchivo = Guid.NewGuid().ToString("N") + extension;
                var carpeta = Path.Combine(_env.WebRootPath, "imagenes");
                Directory.CreateDirectory(carpeta);
                var rutaArchivo = Path.Combine(carpeta, nombreArchivo);

                using (var stream = new FileStream(Path.Combine(carpeta, nombreArchivo), FileMode.Create))
                {
                    await Foto.CopyToAsync(stream);
                }

                var vendedorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    "INSERT INTO productos (vendedor_id, titulo, descripcion, precio, imagen)" +
                    "VALUES (@vendedor_id, @titulo, @descripcion, @precio, @imagen)", conn);

                cmd.Parameters.AddWithValue("@vendedor_id", vendedorId);
                cmd.Parameters.AddWithValue("@titulo", Titulo);
                cmd.Parameters.AddWithValue("@descripcion", (object?)Descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@precio", Precio);
                cmd.Parameters.AddWithValue("@imagen", nombreArchivo);

                await cmd.ExecuteNonQueryAsync();

                return RedirectToPage("/Productos");
            }
            catch (Exception ex)
            {
                Mensaje = "Error al publicar el producto: " + ex.Message;
                return Page();
            }

        }
    }
}

