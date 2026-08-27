# 03 — Design

> Viết ở Phase 1, sau khi đã có `01-requirements.md` và `02-pipeline.md`.
> Ba phần A/B/C nằm chung một file vì chúng thay đổi cùng lúc: đổi cách lưu tiến độ là đổi luôn response của API.
> Mọi lựa chọn phải truy ngược về một mã FR/NFR.

---

# A. Kiến trúc

## A1. Các lực ép từ requirements

Kiến trúc không chọn theo sở thích, mà bị ép bởi ràng buộc. Liệt kê trước, rồi mới chọn.

| Lực ép | Từ | Ép kiến trúc theo hướng nào |
|---|---|---|
| Một lời gọi mất 10–30s+ | FR-25 | HTTP request **không được** đợi tới lúc xong → chạy nền, trả về ngay |
| Phải resume sau restart | FR-23 | Toàn bộ state phải **nằm trên disk**, không chỉ trong RAM |
| Không được gọi trùng | FR-24 | Phải có bước "claim" nguyên tử trước khi gọi Gemini |
| Storage là file phẳng | NFR-01 | Không `UPDATE ... WHERE`, không transaction → tự lo bằng lock trong process |
| Chỉ gửi text sách một lần | FR-29 | Phải lưu con trỏ ngữ cảnh vào state của project |
| Right-sized | NFR-06 | Không Repository interface, không MediatR, không CQRS |
| ☐ … | | |

## A2. Sơ đồ tổng thể

```
┌─────────────────┐        HTTP/JSON        ┌──────────────────────────────┐
│  client (React) │ ──────────────────────▶ │  server (ASP.NET Core)       │
│                 │ ◀────────────────────── │                              │
│  polling 2s     │                         │  Endpoints                   │
└─────────────────┘                         │        │                     │
                                            │        ▼                     │
                                            │  PipelineService             │
                                            │     │            │           │
                                            │     ▼            ▼           │
                                            │  ProjectStore  IGeminiClient │
                                            │     │              │         │
                                            └─────┼──────────────┼─────────┘
                                                  ▼              ▼
                                            data/*.json     Gemini REST
                                            data/images/    (hoặc Fake)
```

## A3. Cấu trúc project — DECISION

| Cách | Được | Mất | Khi nào chọn |
|---|---|---|---|
| A. Một project `server`, chia theo thư mục (`Storage/`, `Gemini/`, `Pipeline/`) | Ít ma sát, dễ nhìn toàn cảnh | Không có ranh giới cứng, dependency dễ chảy ngược | Scope nhỏ, một người làm |
| B. Nhiều project `Api / Application / Infrastructure` | Ranh giới do compiler ép | Nhiều file, nhiều mapping, quá nặng cho ~15 endpoint | Team lớn, domain phức tạp |

**Chốt:** ☐ A ☐ B — lý do: ☐ …

> Tôi đã dùng layered + Repository ở dự án Bookstore. Ở đây `ProjectStore` **chính là** repository rồi — thêm một interface nữa lên trên nó là abstraction rỗng.

## A4. Chạy việc dài 10–30s — DECISION

| Cách | Được | Mất |
|---|---|---|
| A. `Task.Run` fire-and-forget + tự tạo DI scope | Ít code nhất | Không hàng đợi, shutdown là mất việc, khó quan sát |
| B. `BackgroundService` + `Channel<T>` | Có hàng đợi, shutdown lịch sự, giới hạn được số việc song song | Thêm ~50 dòng và một khái niệm mới |

Cả hai đều thoả FR-23 **nếu** state được persist trước khi chạy — mất việc thì project ở trạng thái "running" và cơ chế stuck (FR-27) sẽ cứu.

**Chốt:** ☐ A ☐ B — lý do: ☐ …

## A5. Sequence "user bấm Run step N"

```
Client                 API                    Store                 Pipeline            Gemini
  │  POST .../steps/2/run │                     │                      │                  │
  ├──────────────────────▶│                     │                      │                  │
  │                       │ claim(projectId, 2) │                      │                  │
  │                       ├────────────────────▶│  ┌─ lock ─────────┐  │                  │
  │                       │                     │  │ đọc project    │  │                  │
  │                       │                     │  │ check hợp lệ?  │  │                  │
  │                       │                     │  │ set running=2  │  │                  │
  │                       │                     │  │ ghi atomic     │  │                  │
  │                       │◀────────────────────┤  └────────────────┘  │                  │
  │      202 / 409        │                     │                      │                  │
  │◀──────────────────────┤                     │                      │                  │
  │                       │  chạy nền ──────────┼─────────────────────▶│  gọi API         │
  │  GET /projects/{id}   │                     │                      ├─────────────────▶│
  ├──────────────────────▶│  (polling mỗi 2s)   │                      │◀─────────────────┤
  │◀──────────────────────┤                     │  lưu kết quả + xong  │                  │
```

**Điểm mấu chốt:** khối `lock` thay thế cho câu `UPDATE ... WHERE completedSteps = N-1 AND runningStep IS NULL` mà database sẽ làm giùm. Không có nó, hai request đồng thời cùng đọc thấy "rảnh" và cùng gọi Gemini → vi phạm FR-24.

## A6. Giả định đã chấp nhận

- [ ] Chạy **một process duy nhất**. Lock trong bộ nhớ không bảo vệ được nhiều instance. Ghi rõ trong README.
- [ ] Không có auth thật — email là danh tính.
- [ ] Không có migration cho JSON: đổi schema là phải xử lý file cũ bằng tay hoặc xoá `data/`.
- [ ] ☐ …

---

# B. Data model

## B1. Database từng làm giùm những gì?

Liệt kê cho rõ mình đang mất gì khi bỏ database:

| Database cho | JSON file có? | Tôi phải tự làm gì |
|---|---|---|
| Ghi nguyên tử | ✗ | Ghi file tạm rồi rename |
| `UPDATE ... WHERE` (compare-and-set) | ✗ | Đọc-kiểm-tra-ghi bên trong một lock |
| Transaction nhiều bảng | ✗ | Thiết kế sao cho mỗi thao tác chỉ chạm **một** file |
| Query / index | ✗ | Quét thư mục (chấp nhận được ở scope này) |
| Kiểu dữ liệu, ràng buộc | ✗ | Validate trong code |
| Concurrency nhiều process | ✗ | **Không có** → chấp nhận giả định một process |

## B2. Layout thư mục — DECISION

**A — phẳng theo loại:**
```
data/
  users/<userKey>.json
  projects/<projectId>.json
  images/<projectId>/3-0.png
```

**B — lồng theo user:**
```
data/
  users/<userKey>/
    user.json
    projects/<projectId>/
      project.json
      book.txt
      images/3-0.png
```

| | A | B |
|---|---|---|
| Liệt kê project của user | quét toàn bộ | quét một thư mục |
| Xoá user | phải lọc | `Directory.Delete` |
| Đổi email | không ảnh hưởng đường dẫn | phải đổi tên thư mục |
| Độ phức tạp đường dẫn | thấp | cao hơn |

**Chốt:** ☐ A ☐ B

**`userKey` sinh từ email bằng cách nào?** Email chứa `@`, `.`, có thể chứa `..` hoặc `/` nếu ai đó cố tình → **path traversal**.
☐ hash SHA-256 ☐ slug + whitelist ký tự ☐ sinh GUID riêng làm userId

## B3. Schema `project.json` (nháp)

```json
{
  "id": "9c1f...",
  "userEmail": "oanh@example.com",
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

**`bookText` để trong file này hay tách ra `book.txt`?** Sách có thể vài trăm KB, mà mỗi lần cập nhật tiến độ là ghi lại **toàn bộ** file. Tách ra thì mỗi lần ghi nhẹ hơn nhiều.
**Chốt:** ☐ trong JSON ☐ tách `book.txt`

## B4. Mô hình hoá tiến độ — DECISION

| Cách | Được | Mất |
|---|---|---|
| A. `completedSteps: int` + `runningStep: int?` | Đơn giản; check thứ tự chỉ là `completedSteps == step - 1` | Giả định pipeline tuyến tính; không lưu lịch sử từng lần thử |
| B. Mảng 5 phần tử `[{step, status, startedAt, error}]` | Diễn đạt nhiều hơn; mở đường cho retry history | Nhiều state hơn = nhiều chỗ sai hơn; phải tự giữ bất biến "không có lỗ hổng giữa các bước done" |

**Chốt:** ☐ A ☐ B — bắt buộc ghi vào `DECISIONS.md`.

## B5. Atomic write

Ghi thẳng đè lên file cũ là sai:

```csharp
// SAI
await File.WriteAllTextAsync(path, json);
```

`WriteAllText` **truncate file về 0 byte trước**, rồi mới ghi. App chết ở giữa → file còn lại là JSON cụt → mất trắng project đó.

Đúng:
```
1. ghi toàn bộ nội dung vào path + ".tmp"
2. flush xuống đĩa
3. File.Move(tmp, path, overwrite: true)   ← rename là thao tác nguyên tử ở mức filesystem
```

Sau bước 3, file hoặc là bản cũ nguyên vẹn, hoặc là bản mới nguyên vẹn. Không bao giờ nửa vời.

## B6. Lock — thay thế cho `UPDATE ... WHERE`

Đoạn nguy hiểm là **read → check → write**:

```
Request 1: đọc (runningStep = null) ─┐
Request 2: đọc (runningStep = null) ─┤  cả hai đều thấy "rảnh"
Request 1: ghi runningStep = 2       │
Request 2: ghi runningStep = 2       ┘  → gọi Gemini 2 lần → vi phạm FR-24
```

Cả ba thao tác phải nằm trong cùng một vùng loại trừ lẫn nhau:

```csharp
// Ý tưởng — chưa phải code cuối
private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

public async Task<bool> UpdateAsync(Guid id, Func<Project, bool> mutate)
{
    var gate = _locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
    await gate.WaitAsync();
    try
    {
        var project = await ReadAsync(id);      // đọc
        if (!mutate(project)) return false;      // kiểm tra + sửa; false = không hợp lệ
        await WriteAtomicAsync(project);         // ghi
        return true;
    }
    finally { gate.Release(); }
}
```

- **Vì sao `SemaphoreSlim` chứ không `lock`?** `lock` (Monitor) gắn với **thread**; `await` có thể trả về trên thread khác → release nhầm thread. Trình biên dịch C# không cho `await` bên trong `lock`.
- **Vì sao khoá theo project chứ không toàn cục?** Hai user chạy hai project khác nhau không có lý do phải chờ nhau. Cái giá: dictionary chỉ lớn dần, không bao giờ dọn — chấp nhận ở scope này, nhưng phải **biết** là mình đang chấp nhận.
- **Chốt:** ☐ theo project ☐ toàn cục

## B7. Cạm bẫy phải nhớ

- [ ] `ProjectStore` phải là **singleton** (hoặc dictionary `static`). Nếu scoped, mỗi request có dictionary riêng → lock vô dụng.
- [ ] Lưu ảnh: ghi **file ảnh trước**, cập nhật JSON sau. Ngược lại thì JSON trỏ tới file không tồn tại.
- [ ] `GET /api/images/{id}`: đường dẫn chỉ lấy từ JSON, **không** ghép từ input user.
- [ ] `DateTime` luôn UTC, serialize ISO-8601 có `Z`.
- [ ] `JsonSerializerOptions` phải `static readonly` — tạo mới mỗi lần gọi rất chậm.

## B8. Test bắt buộc cho tầng này

- [ ] Ghi rồi đọc lại ra đúng giá trị.
- [ ] 50 `UpdateAsync` song song lên cùng project → không mất update nào.
- [ ] Hai claim đồng thời cho cùng một bước → đúng **một** cái thành công.
- [ ] File `.tmp` bị bỏ lại không làm hỏng lần đọc kế tiếp.

---

# C. API contract

Base URL dev: `http://localhost:5050`

## C0. Nhận diện user — DECISION

| Cách | Được | Mất |
|---|---|---|
| A. Header `X-User-Email` | Đơn giản nhất, curl dễ | Ai cũng giả mạo được — **không phải bảo mật** |
| B. Cookie phiên ký | Giống thật hơn | Thêm hạ tầng cho thứ đề nói rõ là không cần |

**Chốt:** ☐ A ☐ B. Nếu A → ghi rõ trong README rằng đây không phải authentication.

## C1. `POST /api/auth`

```
→ { "email": "oanh@example.com", "name": "Oanh" }
← 200 { "userId": "…", "email": "…", "name": "…" }
```
- Email chuẩn hoá: trim + lowercase.
- Email đã có → trả về. Có cập nhật tên không? ☐ có ☐ không
- `400` nếu thiếu email/tên hoặc email không hợp lệ.

## C2. `POST /api/projects`

```
→ { "title": "…", "bookText": "…" }
← 201 { project summary }
```
- Upload `.txt`: FE đọc file thành string, hay gửi `multipart/form-data`? ☐ FE đọc ☐ multipart
- `400` nếu title rỗng, bookText rỗng, hoặc vượt giới hạn (bao nhiêu? ☐ …).

## C3. `GET /api/projects`

```
← 200 [ { "id","title","createdAt","completedSteps","runningStep","failedStep" } ]   // mới nhất trước
```
Trạng thái hiển thị ở UI: server trả sẵn một trường `status`, hay client tự tính? ☐ server ☐ client
(Server tính thì UI không lệch logic; client tính thì payload gọn hơn.)

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

**DECISION:** ☐ endpoint riêng `POST .../steps/{step}/reset` ☐ `run` tự cướp quyền khi quá ngưỡng ☐ cả hai
Ngưỡng bao nhiêu phút? ☐ … (ảnh mất 30s+, đừng đặt quá ngắn)

## C7. `GET /api/images/{id}`

Trả file nhị phân kèm đúng `Content-Type`. `404` nếu không có id hoặc file mất.
**Bắt buộc:** đường dẫn chỉ lấy từ metadata trong JSON.

## C8. Định dạng lỗi

Chốt một dạng duy nhất rồi dùng nhất quán toàn API:
```json
{ "error": "STEP_NOT_AVAILABLE", "message": "Step 3 cannot start before step 2 completes." }
```
`error` cho máy đọc (client đổi UI theo mã này), `message` cho người đọc.
**Chốt:** ☐ dạng này ☐ chỉ trả chuỗi ☐ ProblemDetails (RFC 7807)
