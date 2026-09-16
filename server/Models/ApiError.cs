namespace server.Models;

//format lỗi dùng chung toàn api, C8
public record ApiError(string Error, string Message);