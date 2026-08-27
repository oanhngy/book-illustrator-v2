# Learning log

> Mục đích chính của dự án này. Gặp thứ chưa biết → dừng lại → học → ghi vào đây **bằng lời của mình**.
> Không viết lại được bằng lời của mình = chưa hiểu.
> Entry mới nhất ở trên cùng.

---

## Mẫu

```
## [YYYY-MM-DD] Tên khái niệm

**Gặp ở đâu:** file / task nào, phase mấy
**Trước đó tôi nghĩ:** (kể cả khi nghĩ sai — chỗ này quý nhất)
**Thực ra là:** giải thích bằng lời của mình, 3–5 câu
**Neo vào cái đã biết:** giống/khác gì với thứ tôi đã dùng trong C# / ASP.NET
**Kiểm chứng:** tôi chứng minh mình hiểu bằng cách nào (viết test? phá cho nó lỗi rồi xem?)
**Còn mờ:** phần nào vẫn chưa rõ
```

---

## Danh sách chủ đề dự kiến

Tick khi đã viết entry. Bổ sung thêm khi gặp cái mới.

**Backend / .NET**
- [ ] Minimal API — khác gì Controller, khi nào chọn cái nào
- [ ] DI lifetime: singleton vs scoped vs transient — và vì sao chọn sai làm lock vô dụng
- [ ] `System.Text.Json` — naming policy, vì sao `JsonSerializerOptions` phải static
- [ ] Atomic write bằng temp file + rename — vì sao rename nguyên tử mà write thì không
- [ ] `SemaphoreSlim` vs `lock` — vì sao không `await` được trong `lock`
- [ ] `ConcurrentDictionary.GetOrAdd`
- [ ] Chạy việc nền: `Task.Run` vs `BackgroundService` + `Channel`
- [ ] `IHttpClientFactory` / typed client — vấn đề socket exhaustion
- [ ] `CancellationToken` — ai tạo, ai truyền, ai kiểm tra
- [ ] `Results.File`, phục vụ file nhị phân, path traversal

**Gemini / AI API**
- [ ] Structured output bằng JSON schema
- [ ] Nối ngữ cảnh giữa nhiều lượt gọi mà không gửi lại nội dung
- [ ] Sinh ảnh: định dạng trả về, cách giữ nhân vật nhất quán
- [ ] Rate limit và quota — free tier của model ảnh khác model text

**Frontend**
- [ ] `useEffect` cleanup — vì sao thiếu nó thì interval nhân lên
- [ ] Polling: khi nào bắt đầu, khi nào dừng
- [ ] `AbortController`
- [ ] Sống thiếu TypeScript — bù lại bằng gì

**Testing**
- [ ] `WebApplicationFactory` — app chạy trong bộ nhớ
- [ ] Cô lập thư mục tạm giữa các test
- [ ] Vitest + React Testing Library

**Khái niệm chung**
- [ ] Race condition — tự tái hiện được một cái
- [ ] Atomicity, và vì sao "read-check-write" không nguyên tử
- [ ] State machine và bất biến (invariant)
- [ ] Idempotency
- [ ] Right-sizing — khi nào một abstraction là thừa

---

## Entries

<!-- Bắt đầu ghi từ đây -->
