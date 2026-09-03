# DECISIONS.md

> Chỉ ghi **quyết định**, không ghi nhật ký công việc — phần đó git history lo.
> Mỗi mục: ai đề xuất, ai phản đối, chốt ở đâu, **cái giá đã chấp nhận**.
> Cái giá là phần quan trọng nhất. Một quyết định không có cái giá nào là một quyết định chưa được suy nghĩ.

---

## Mẫu

```
## <Tên quyết định>

**Bối cảnh:** vì sao phải quyết chuyện này, ràng buộc nào ép.
**Các lựa chọn:** A / B / C — mỗi cái được gì.
**Chốt:** …
**Cái giá chấp nhận:** …
**Sẽ xem lại khi:** điều kiện nào làm quyết định này sai đi.
```

---

## Cần có ít nhất các mục sau

- [ ] **Stack và storage** — vì sao .NET + React JS; vì sao JSON file thay vì SQLite, dù bản trước đã dùng SQLite thành công. Cái giá: không transaction, không query, chỉ đúng với một process.
- [ ] **Mô hình hoá tiến độ pipeline** — `completedSteps` + `runningStep`, hay mảng 5 trạng thái?
- [ ] **Chống chạy trùng khi refresh / double-click / hai tab** — lock ở đâu, vì sao client-side là không đủ.
- [ ] **Gửi text sách một lần** — chọn cơ chế nào, con trỏ ngữ cảnh persist ra sao, hết hạn thì sao.
- [ ] **Xử lý bước treo** — ngưỡng bao nhiêu, ai phát hiện.
- [ ] **Right-sizing** — những abstraction đã **cố ý không** thêm (Repository interface, MediatR, tầng Application riêng) và vì sao.

## Chỗ tôi override Claude

> Đề bài gốc coi đây là tín hiệu mạnh nhất. Ở dự án tự học, nó có giá trị khác: mỗi lần tôi thấy lời khuyên của Claude sai/thừa/quá phức tạp và tự đưa ra lựa chọn khác — đó là bằng chứng tôi đã thực sự hiểu, không phải chép.

- [ ] Override #1: …
- [ ] Override #2: …
- [ ] Override #3: …

## Chỗ Claude bắt lỗi tôi

- [ ] …
