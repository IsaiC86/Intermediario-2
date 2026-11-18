using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace intermediarioDB.Pages
{
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _config;
        public LoginModel(IConfiguration config) => _config = config;

        [BindProperty]
        [Required(ErrorMessage = "Escribe tu correo")]
        [EmailAddress(ErrorMessage = "Correo no valido")]
        [Display(Name = "Correo")]
        public string Correo { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Escribe tu contraseña")]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = "";
        public string Mensaje { get; set; } = "";

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            if (!ModelState.IsValid) return Page();

            try
            {
                using var conn = new MySqlConnection(_config.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                using var cmd = new MySqlCommand(
                    "SELECT id, nombre_completo, passwordHash, estado_verificacion, es_admin " +
                    "FROM usuarios WHERE correo = @correo", conn);

                cmd.Parameters.AddWithValue("@correo", Correo);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var id = reader.GetInt32("id");
                    var nombre = reader.GetString("nombre_completo");
                    var hash = reader.GetString("passwordHash");
                    var estado = reader.GetString("estado_verificacion");
                    var esAdmin = reader.GetBoolean("es_admin");   //cuando se cambia a admin

                    var resultado = new PasswordHasher<string>()
                        .VerifyHashedPassword(Correo, hash, Password);

                    if (resultado != PasswordVerificationResult.Failed)
                    {
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                            new Claim(ClaimTypes.Name, nombre),
                            new Claim(ClaimTypes.Email, Correo),
                            new Claim("estado_verificacion", estado),
                            new Claim("es_admin", esAdmin ? "1" : "0")
                        };
                        var identity = new ClaimsIdentity(claims, "Cookies");
                        await HttpContext.SignInAsync("Cookies", new ClaimsPrincipal(identity));

                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                            return LocalRedirect(returnUrl);
                        return RedirectToPage("/Index");
                    }
                }

                Mensaje = "Correo o contraseña incorrectos";
            }
            catch (Exception ex)
            {
                Mensaje = "Error: " + ex.Message;
            }

            return Page();

        }
    }
}



