using server.Models;
namespace server.Storage;

public class UserStore
{
    private readonly string _usersDir;
    public UserStore(string usersDir="data/users")
    {
        _usersDir=usersDir;
    }

    private string PathFor(string email) => Path.Combine(_usersDir, $"{UserKey.From(email)}.json");
    public Task<User?> GetAsync(string email) => JsonStore.ReadAsync<User>(PathFor(email));

    //upsert: chưa có--> tạo, có rồi --> update tên (C1)
    public async Task<User> UpsertAsync(string email, string name)
    {
        var normalizedEmail=email.Trim().ToLowerInvariant();
        var existing=await GetAsync(normalizedEmail);

        var user=existing??new User
        {
            Email=normalizedEmail,
            CreatedAt=DateTime.UtcNow,
        };
        user.Name=name; //luôn update tên mới, kể cả đã tồn tại
        await JsonStore.WriteAsync(PathFor(normalizedEmail), user);
        return user;
    }
}