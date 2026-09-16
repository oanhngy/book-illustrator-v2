using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace server.Storage;

public class UserKey
{
    public static string From(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();

        //slug: giữ chữ, số, ký tự khác thay bằng -
        var slug=Regex.Replace(normalized, "[^a-z0-9]+", "-").Trim('-');

        //hash: 8 ký tự đầu, chống trùng
        var hashBytes=SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var hash=Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
        
        return $"{slug}-{hash}";
    }
    
}