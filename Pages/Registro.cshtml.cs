using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace intermediarioDB.Pages
{
    public class RegistroModel : PageModel
    {
        private readonly IConfiguration _config;
        public RegistroModel(IConfiguration config) => _config = config;

        [BindProperty]
        [Required(ErrorMessage = "Escribe tu nombre completo")]
        [StringLength(100)]
        [Display(Name = "Nombre completo")]
        public string NombreCompleto { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Escribe tu correo")]
        [EmailAddress(ErrorMessage = "Correo no válido")]
        [StringLength(100)]
        [Display(Name = "Correo electrónico")]
        public string Correo { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Escribe una contraseña")]
        [MinLength(8, ErrorMessage = "Mínimo 8 caracteres")]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Confirma tu contraseña")]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden")]
        [Display(Name = "Confirmar contraseña")]
        public string ConfirmarPassword { get; set; } = "";

        [BindProperty]
        [Phone(ErrorMessage = "Teléfono no válido")]
        [StringLength(15)]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; }

        public string Mensaje { get; set; } = "";

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            try
            {
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                using (var check = new MySqlCommand("SELECT COUNT(*) FROM usuarios WHERE correo = @correo", conn))
                {
                    check.Parameters.AddWithValue("@correo", Correo);
                    var existe = Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;
                    if (existe)
                    {
                        ModelState.AddModelError(nameof(Correo), "Ese correo ya está registrado");
                        return Page();
                    }
                }

                var hash = new PasswordHasher<string>().HashPassword(Correo, Password);

                using var cmd = new MySqlCommand(
                    "INSERT INTO usuarios (nombre_completo, correo, passwordHash, telefono) " +
                    "VALUES (@nombre, @correo, @hash, @tel)", conn);
                cmd.Parameters.AddWithValue("@nombre", NombreCompleto);
                cmd.Parameters.AddWithValue("@correo", Correo);
                cmd.Parameters.AddWithValue("@hash", hash);
                cmd.Parameters.AddWithValue("@tel", (object?)Telefono ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();

                ModelState.Clear();
                NombreCompleto = Correo = Password = ConfirmarPassword = "";
                Telefono = null;
                Mensaje = "Registro exitoso. Tu cuenta quedó pendiente de verificación.";
            }
            catch (Exception ex)
            {
                Mensaje = "Error: " + ex.Message;
            }

            return Page();
        }
    }
}
