namespace server.Tests.Gemini;

//hander giả dùng chung mọi test để test GeminiClient k gọi thật
//k Moq đc vì SendAsync nằm trên HttpMessageHandler(protected)
public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    //mỗi test tự định nghĩa cách trloi qia responder, derive theo input, k giữa state chung giữa các test
    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder=responder;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_responder(request));
    }
}