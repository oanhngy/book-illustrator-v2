using System.Text.RegularExpressions;
using server.Models;
using server.Storage;
namespace server.Endpoints;

//DTO cho POST /api/auth (C1)
//record vì chỉ mang data, k hành vi
public record AuthRequest(string Email, string Name);
public record AuthResponse(string UserId, string Email, string Name);

public static class AuthEndpoints
{
    //extension method gom riêng route auth --> để program.cs gọn
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth", async (AuthRequest request, UserStore userStore) =>
        {
            if(string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new ApiError("INVALID_INPUT", "Email and name are required"));
            }

            //regex cơ bản
            if(!Regex.IsMatch(request.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                return Results.BadRequest(new ApiError("INVALID_EMAIL", "Email format is invalid"));
            }
            var user=await userStore.UpsertAsync(request.Email, request.Name);

            var userId=UserKey.From(user.Email); //userId=userKey(slug+hash), k có tầng tra cứu guid riêng
            return Results.Ok(new AuthResponse(userId, user.Email, user.Name));
        });
    }
}