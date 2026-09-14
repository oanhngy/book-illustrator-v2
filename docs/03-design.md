# 03 — Design
---

# A. Kiến trúc
## A1. Các lực ép từ requirements

Kiến trúc không chọn theo sở thích, mà bị ép bởi ràng buộc. Liệt kê trước, rồi mới chọn.

| Lực ép | Từ | Ép kiến trúc theo hướng nào |
|---|---|---|
| Một lời gọi mất 10–30s+ | docs/pipeline | HTTP request **không được** đợi tới lúc xong → chạy nền, trả về ngay |
| Phải resume sau restart | FR-23 | Toàn bộ state phải **nằm trên disk**, không chỉ trong RAM |
| Không được gọi trùng | FR-24, FR-22 | Phải có bước "claim" nguyên tử trước khi gọi Gemini |
| Storage là file phẳng | NFR-01 | Không `UPDATE ... WHERE`, không transaction → tự lo bằng lock trong process |
| Chỉ gửi text sách một lần | FR-29 | Phải lưu con trỏ ngữ cảnh vào state của project |
| Right-sized | NFR-06 | Không Repository interface, không MediatR, không CQRS |
| Đọc được toàn văn sách bất kỳ lúc nào | FR-14 | bookText phải sớng bền, tách khỏi state hay đổi --> xem B3 |
| Ảnh phải hiện từng tấm khi sinh | FR-32 | PipelineService phải ghi xuống Store sau mỗi ảnh (nhiều lần UpdateAsync) |
| Bước treo phải có đường thoát cho user, không sửa tay | FR-27 | Cần lưu runningSince để tính đã chạy bao lâu, cần cơ chế/endpoint reset (C6) |
| Ảnh + text sách phục vụ qua API của mình, không S3/CDN | NFR-02 | Bắt buộc có endpoint tự phục vụ file nhị phân trực tiếp từ đĩa, không redirect dịch vụ ngoài | 

## A2. Sơ đồ tổng thể (Component Diagram)

```
┌─────────────────┐        HTTP/JSON        ┌────────────────────────────────────────────────────────────── -┐
│  client (React) │ ──────────────────────▶ │  server (ASP.NET Core)                                         │
│                 │ ◀────────────────────── │                                                                │
│  polling 2s     │                         │           -------------Endpoints - - - - - - - - -             │
└─────────────────┘                         │           │                 │                     ┊            │
                                            │ (ghi item)│         (claim) │                     ┊ (đọc/poll, │
                                            │           ▼                 ▼                     ┊  gọi thẳng │
                                            │      Channel<T>       PipelineService             ┊  Store,    │
                                            │           ▲               │       │               ┊  không qua │
                                            │     (đọc) │               │       ▼               ┊  Pipeline) │
                                            │           │               │  IGeminiClient        ┊            │
                                            │  ┌────────┴─────────-┐    │       │               ┊            │
                                            │  │ BackgroundService │    │       │               ┊            │
                                            │  │ (tự tạo scope,    │────┘       │               ┊            │
                                            │  │  gọi RunStepAsync)│            │               ┊            │
                                            │  └───────────────────┘            │               ┊            │
                                            │           │                       │               ┊            │
                                            │           ▼                       │               ▼            │
                                            │      ProjectStore ◄───────────────┴───────────────┘            │
                                            │           │                       │                            │
                                            └───────────┼───────────────────────┼────────────────────────--──┘
                                                        ▼                       ▼
                                                   data/*.json               Gemini REST
                                                   data/images/              (hoặc Fake)
```

## A3. Cấu trúc project
Một project chia theo thư mục
Quy tắc Dependency: Endpoint --> Pipeline --> {Storage, Gemini} cho thao tác ghi/orchestration (claim, run step). Storage không được biết khái niệm nghiệp vụ pipeline (only CRUD qua Get/Save/Update(mutate)), việc đọc (GET) được gọi thẳng Store không vi phạm nguyên tắc
Trade-offs ở `DECISIONS.md` ##1

**Cân nhắc lại:** đề xuất thêm `IProjectStore` interface để dễ mock trong unit test — giữ nguyên, không thêm. Unit test `PipelineService` dùng `ProjectStore` thật trỏ `data/` tạm, không mock — test chạy qua atomic write + lock thật, giá trị chứng minh cao hơn mock. Chi tiết ở `DECISIONS.md`.

## A4. Chạy việc dài 10–30s
Luồng: Nhận request --> Ghi nhận state --> Trả HTTP 202 Accepted ASAP --> Chạy việc gọi Gemini ở chế độ nền
BackgroundService + Channel<T>= nền tảng chuẩn server-sice --> Học thêm trong Phase 6
Trade-offs ở `DECISIONS.md` ##5

## A5. Sequence "user bấm Run step N"

```
Client           API            Channel    BackgroundService     Pipeline          Store           Gemini
  |               |                |               |                 |               |               |
  | 1. POST .../steps/2/run        |               |                 |               |               |
  |-------------->|                |               |                 |               |               |
  |               | 2. ClaimStepAsync(id, 2)       |                 |               |               |
  |               |------------------------------------------------->|               |               |
  |               |                |               |                 | 3. UpdateAsync|               |
  |               |                |               |                 |-------------->|               |
  |               |                |               |                 |               | ┌─ lock ─────┐|
  |               |                |               |                 |               | │ đọc project│|
  |               |                |               |                 |               | │ check      │|
  |               |                |               |                 |               | │ running=2  │|
  |               |                |               |                 |               | │ ghi atomic │|
  |               |                |               |                 |               | └────────────┘|
  |               |                |               |                 | 4. OK         |               |
  |               |                |               |                 |<--------------|               |
  |               | 5. OK (claimed)|               |                 |               |               |
  |               |<-------------------------------------------------|               |               |
  |               |                |               |                 |               |               |
  |               | 6. Write(id, 2)|               |                 |               |               |
  |               |--------------->|               |                 |               |               |
  | 7. 202 Accepted                |               |                 |               |               |
  |<--------------|                |               |                 |               |               |
=============================================================================================================
                                             [ LUỒNG NGẦM ]
=============================================================================================================
  |               |                |               |                 |               |               |
  |               |                | 8. Read() liên tục từ khi app start             |               |
  |               |                |<--------------|                 |               |               |
  |               |                |               |                 |               |               |
  |               |                |   [Tự tạo IServiceScopeFactory.CreateScope()]   |               |
  |               |                |   [Resolve PipelineService từ Scope mới]        |               |
  |               |                |               |                 |               |               |
  |               |                |               9. RunStepAsync(id, 2)            |               |
  |               |                |               |---------------->|               |               |
  |               |                |               |                 | 10. Gọi API   |               |
  |               |                |               |                 |------------------------------>|
  |               |                |               |                 | 11. Trả KQ    |               |
  |               |                |               |                 |<------------------------------|
  |               |                |               |                 | 12. Lưu, running=null (atomic)|
  |               |                |               |                 |-------------->|               |
-------------------------------------------------------------------------------------------------------------
                                            [ LUỒNG POLLING ]
-------------------------------------------------------------------------------------------------------------
  |               |                |               |                 |               |               |
  | GET /projects/{id}             |               |                 |               |               |
  |-------------->|                |               |                 |               |               |
  |               | đọc project    |               |                 |               |               |
  |               |----------------------------------------------------------------->|               |
  |               | trả state      |               |                 |               |               |
  |               |<-----------------------------------------------------------------|               |
  | 200 OK        |                |               |                 |               |               |
  |<--------------|                |               |                 |               |               |
```

**Điểm mấu chốt:** khối `lock` thay thế cho câu `UPDATE ... WHERE completedSteps = N-1 AND runningStep IS NULL` mà database sẽ làm giùm. Không có nó, hai request đồng thời cùng đọc thấy "rảnh" và cùng gọi Gemini → vi phạm FR-24.

## A6. Giả định đã chấp nhận
- Chạy 1 process duy nhất. Lock trong bộ nhớ không bảo vệ được nhiều instance, có trong README
- KHông auth thật, email là danh tính
- Không migration cho JSON: đổi schema là phải xử lý file cũ manual hoặc xóa data/ --> Chấp nhận cho dự án cá nhân
- Channel<T> chỉ tồn tại trong bộ nhớ, không bền. Nếu server restart giữa lúc item còn trong hàng đợi (chưa xuống BackgroundService), item đó sẽ mất, vì runningStep/runningSince đã ghi xuống disk trước enqueue (xem A5 bước 2-6), cơ chế phát hiện bước treo được cứu khi user thấy bước "đang chạy" quá lâu và tự force retry
- KHông hỗ trợ đổi email sau khi tạo tài khoản, out of scope của requirements
- Context phía Gemini có TTL 48h, hết hạn phải re-construct, chi tiết docs/02-pipeline.md phần 2

---

# B. Data model
## B1. Không xài Database mất gì - Cần làm?
- Ghi nguyên tử --> Temp file then rename
- UPDATE...WHERE --> Read-Check-Write trong 1 lock
- Transaction nhiều bảng --> Thiết kế mỗi thao tác chỉ chạm 1 file
- Query/Index --> Quét thư mục (chấp nhận ở scope này)
- Data type, constraint --> Validate trong code
- Concurrency nhiều process --> Chấp nhận giả định 1 process

## B2. Layout thư mục — DECISION
Mô hình=Data model phẳng
userKey= slug+Hash SHA-256 (8 ký tự đầu)
--> project.json bắt buộc tự mang userEmail vì path không còn làm việc đó thay (xem B3)
Trade-offs ở `DECISIONS.md` ##2

## B3. Schema `project.json`

```json
{
  "id": "9c1f...",
  "userEmail": "oanh@example.com", //bắt buộc
  "title": "The Wind in the Willows",
  "createdAt": "2026-08-27T04:00:00Z",

  "completedSteps": 2,
  "runningStep": null,
  "runningSince": null,
  "failedStep": null,
  "lastError": null,

  "contextRef": "…",

  "style": "watercolour, soft edges, warm palette",
  "characters": [ { "name": "Mole", "imagePrompt": "…" } ],
  "chapters":   [ { "title": "…", "summary": "…", "imagePrompt": "…" } ],
  "images":     [ { "id": "…", "step": 3, "index": 0, "path": "…", "mimeType": "image/png", "createdAt": "…" } ]
}
```

**bookText để trog file hay tách?**
--> Tách riêng vì field trong đó thay đổi liên tục, để chung thì phải chép lại toàn bộ nội dung còn lại dù không thay đổi nhiều lần --> tốn kém + write amplification không cần thiết 
Trade-off: khi trả full detail (C4, cần cả bookText) thì server phải đọc 2 file thay vì 1, nhưng chi phí READ rẻ hơn WRITE, + tần suất thấp --> đáng đánh đổi

**Resume theo item (Portraits/Illustrations) — không đổi schema:**
Khi retry step 3/5, `PipelineService` lọc `project.images.Where(i => i.step == step)` để biết những
`index` (nhân vật/chương) nào đã có ảnh — chỉ gọi Gemini cho phần còn thiếu, không generate lại ảnh
đã có. Tận dụng `images[]` sẵn có, không cần đổi cấu trúc `completedSteps`/`runningStep`. Chi tiết
lý do và option đã cân nhắc ở `DECISIONS.md`.

## B4. Mô hình hoá tiến độ
completedSteps: int + runningStep: int?
Trade-offs ở `DECISIONS.md` ##3

## B5. Atomic write
Không ghi thẳng đè lên file cũ là sai, cách đúng:
```
1. ghi toàn bộ nội dung vào path + ".tmp"
2. flush xuống đĩa
3. File.Move(tmp, path, overwrite: true)   ← rename là thao tác nguyên tử ở mức filesystem
```

Sau bước 3, file hoặc là bản cũ nguyên vẹn, hoặc là bản mới nguyên vẹn. Không bao giờ nửa vời.

## B6. Lock — thay thế cho `UPDATE ... WHERE`
Khóa theo từng project ConcurrentDictionary<Guid, SemaphoreSlim>
- Mọi thao tác cập nhật tiến độ (Read - Mutate - Write) bắt buộc nằm trong khối SemaphoreSlim.WaitAsync() tương ứng từng project
- Thao tác ghi đĩa bắt bước như B5
Trade-offs ở `DECISIONS.md` ##6

## B7. Cạm bẫy phải nhớ

- [x] `ProjectStore` phải là **singleton** (hoặc dictionary `static`). Nếu scoped, mỗi request có dictionary riêng → lock vô dụng.
- [x] Lưu ảnh: ghi **file ảnh trước**, cập nhật JSON sau. Ngược lại thì JSON trỏ tới file không tồn tại.
- [x] `GET /api/images/{id}`: đường dẫn chỉ lấy từ JSON, **không** ghép từ input user.
- [x] `DateTime` luôn UTC, serialize ISO-8601 có `Z`--> múi giờ chuẩn để in case server Mỹ, React ở Nhật, BE Việt thì vẫn giống nhau
- [x] `JsonSerializerOptions` phải `static readonly` — tạo mới mỗi lần gọi rất chậm.

## B8. Test bắt buộc cho tầng này

- [ ] Ghi rồi đọc lại ra đúng giá trị.
- [ ] 50 `UpdateAsync` song song lên cùng project → không mất update nào.
- [ ] Hai claim đồng thời cho cùng một bước → đúng **một** cái thành công.
- [ ] File `.tmp` bị bỏ lại không làm hỏng lần đọc kế tiếp.

---

# C. API contract
Base URL dev: `http://localhost:5050`

## C0. Nhận diện user
Header `X-User-Email`
Trade-offs ở `DECISIONS.md` ##4

## C1. `POST /api/auth`
```
→ { "email": "oanh@example.com", "name": "Oanh" }
← 200 { "userId": "…", "email": "…", "name": "…" }
```
- Email chuẩn hoá: trim + lowercase.
- Email đã có → trả về. **Cập nhật tên, POST/api/auth kiêm luôn sửa tên**, không thêm endpoint mới, thỏa NFR-06 (tái sdung thay vì xây thêm)
- `400` nếu thiếu email/tên hoặc email không hợp lệ.

## C2. `POST /api/projects`
```
→ { "title": "…", "bookText": "…" }
← 201 { project summary }
```
- Upload `.txt`: **FE đọc file thành string**. File .txt chỉ khoảng 10% context window, không cần multipart (also không phát huy được lợi thế)
- `400` nếu title rỗng, bookText rỗng, hoặc vượt giới hạn 500.000 ký tự: Giới hạn tính theo số ký tự, ép ở both FE và server, chọn trần 500.000 ký tự

## C3. `GET /api/projects`

```
← 200 [ { "id","title","createdAt","completedSteps","runningStep","failedStep" } ]   // mới nhất trước
```
Trạng thái hiển thị ở UI: server trả sẵn một trường `status`, hay client tự tính? **Server tính**, BE định nghĩa rõ enum trạng thái và maintain, logic running/failed/completed chỉ viết 1 chỗ duy nhất(FR-30), payload nặng hơn đúng 1 field có giá ít hơn so với việc trùng lặp logic

## C4. `GET /api/projects/{id}`

Trả detail đầy đủ: cộng thêm `bookText`, `style`, `characters`, `chapters`, `images[]`, `lastError`, `canForceRetry`.

- `404` nếu không tồn tại **hoặc** thuộc user khác. Dùng 404 thay 403 để không lộ việc project có tồn tại.
- Endpoint này bị polling 2s → phải rẻ. Ảnh trả **URL**, tuyệt đối không nhúng base64.

## C5. `POST /api/projects/{id}/steps/{step}/run`

Body (chỉ dùng ở bước 1): `{ "style": "…" }` — tuỳ chọn (FR-31).

| Mã | Khi nào |
|---|---|
| `202 Accepted` | Claim thành công, bước đang chạy nền |
| `409 Conflict` | Sai thứ tự, hoặc đang có bước chạy → **đây chính là cơ chế chống gọi trùng** |
| `400` | step ngoài 1–5 |
| `404` | không phải project của user |

Không trả kết quả — client polling `GET /api/projects/{id}`.

## C6. Bước treo (FR-27)
**PHase 8**
**DECISION:** ☐ endpoint riêng `POST .../steps/{step}/reset` ☐ `run` tự cướp quyền khi quá ngưỡng ☐ cả hai
Ngưỡng bao nhiêu phút? ☐ … (ảnh mất 30s+, đừng đặt quá ngắn)

## C7. `GET /api/images/{id}`

Trả file nhị phân kèm đúng `Content-Type`. `404` nếu không có id hoặc file mất.
**Bắt buộc:** đường dẫn chỉ lấy từ metadata trong JSON.

## C8. Định dạng lỗi
1 dạng duy nhất rồi dùng nhất quán toàn API:
```json
{ "error": "STEP_NOT_AVAILABLE", "message": "Step 3 cannot start before step 2 completes." }
```
**Chốt:** như trên=**Custome {error, message}, đúng mục tiêu machine-readable + human-readable, phù hợp right-sizing và ít code
