# PLAN.md — Quy trình thực hiện dự án

> Cách đọc file này: đi tuần tự từ Phase 0. Mỗi phase có **Mục tiêu → Cần học → Việc cần làm → Decision → Definition of Done → Commit**.
> Không nhảy phase. Nhưng được phép quay lại sửa doc của phase trước khi phát hiện mình viết sai — đó là chuyện bình thường.

---

## Tài liệu của dự án

| File | Viết ở phase | Vai trò |
|---|---|---|
| `docs/gradion-assessment-intern-software-engineer.md` | có sẵn | Nguồn duy nhất về **chức năng cần có** |
| `PLAN.md` | có sẵn | Quy trình. File này |
| `CLAUDE.md` | có sẵn | Cách làm việc với Claude |
| `DECISIONS.md` | ghi dần từ Phase 1 | Quyết định + **cái giá đã chấp nhận** |
| `docs/01-requirements.md` | Phase 0 | Đề bài → checklist có mã (FR-xx, NFR-xx). Cái **gì** |
| `docs/02-pipeline.md` | Phase 0 | Ghi chép sau khi chạy notebook: 5 bước gọi API ra sao, chaining ra sao |
| `docs/03-design.md` | Phase 1 | Kiến trúc + data model + API contract. Cái **như thế nào** |
| `docs/learning-log.md` | ghi dần suốt dự án | Khái niệm mới, viết lại bằng lời của mình |
| `README.md` | Phase 11 | Cách chạy, cách test, kiến trúc tóm tắt |
| `TESTING.md` | Phase 10 | Chiến lược test + report một lần chạy thật |

Không tạo trước hàng loạt file rỗng. **Tới phase nào thì tạo file của phase đó.**

Vì sao gộp architecture + data model + API contract vào một file `03-design.md`: ba thứ này thay đổi cùng lúc. Sửa cách lưu tiến độ là sửa luôn response của API — tách ra ba file chỉ làm chúng lệch nhau.

---

## Nguyên tắc xuyên suốt

**1. Spec trước, code sau.** Không gõ dòng code nào cho một tính năng chưa được mô tả trong `docs/`.

**2. Học on-the-go (just-in-time learning).** Deadline 30h không thể áp dụng hoàn toàn, chỉ áp dụng cho những khái niệm thực sự khó/mới. Không học trước cả một cuốn sách. Gặp chỗ không biết thì dừng lại, học **đúng phần đó**, rồi quay lại code. Vòng lặp:

```
Gặp thứ không biết
   ↓
Diễn đạt được câu hỏi cụ thể ("làm sao ghi file JSON mà không hỏng nếu app crash giữa chừng?")
   ↓
Học nhỏ nhất đủ dùng (đọc doc / hỏi Claude / viết file thử nghiệm)
   ↓
Áp vào code thật
   ↓
Giải thích lại cho Claude nghe → nếu ú ớ nghĩa là chưa hiểu, quay lại bước 3
```

**3. Walking skeleton trước, chiều sâu sau.** Cho một đường dây mỏng nhất chạy xuyên FE → API → file trên disk từ rất sớm. Sau đó mới đắp thịt. Không làm xong hoàn hảo backend rồi mới đụng frontend.

**4. Vertical slice.** Mỗi phase làm xong một lát cắt **dùng được**, không phải một tầng kiến trúc.

**5. Right-sizing.** Chỉ thêm abstraction khi đã có **hai** chỗ thật sự cần nó. Không thêm cho thứ chưa ship.

---

## Phase 0 — Hiểu đề & hiểu pipeline

**Mục tiêu:** biết chính xác mình phải xây cái gì, trước khi bàn tới kiến trúc.

**Cần học:** Gemini API làm được gì — structured JSON output, image generation, nối ngữ cảnh giữa các lượt gọi.

**Việc cần làm:**
- [x] Đọc kỹ `docs/gradion-assessment-intern-software-engineer.md`, đánh dấu mọi câu chứa "must" / "required" / "hard requirement".
- [x] Tự chạy notebook Google *Illustrate a book: The Wind in the Willows* (bước 1–5) trên Colab. Bắt buộc — không đoán pipeline.
- [x] **Tạo và viết `docs/01-requirements.md`** — chuyển đề bài thành checklist có mã: FR-xx (chức năng), NFR-xx (phi chức năng), và một mục "ngoài phạm vi".
- [x] **Tạo và viết `docs/02-pipeline.md`** — với mỗi bước trong 5 bước: gọi endpoint nào, gửi gì, nhận gì, ngữ cảnh nối sang bước sau bằng cơ chế nào, cap bao nhiêu. Kèm prompt của từng bước.
- [x] Nếu còn giữ `app-demo.html` của đề: click qua từng màn hình, ghi lại các state nhìn thấy vào `01-requirements.md`.

**Chưa được làm:** chưa chọn kiến trúc, chưa tạo project .NET.

**DoD:** trả lời được, không cần mở đề — "5 bước là gì, cap là bao nhiêu, tại sao chỉ được gửi text sách một lần?"

**Commit:** `docs: requirements checklist from the brief` · `docs: pipeline notes from the colab notebook`

---

## Phase 1 — Thiết kế

**Mục tiêu:** chốt hình dạng hệ thống dựa trên requirements ở Phase 0, có lý do cho từng lựa chọn.

**Cần học:** Minimal API vs Controller; transaction script vs layered; khi nào Repository pattern là thừa.

**Việc cần làm:**
- [x] Liệt kê các **lực ép** từ requirements: gọi AI mất 10–30s, phải resume được, phải chống gọi trùng, storage là file phẳng. Với mỗi lực ép → nó ép kiến trúc theo hướng nào?
- [x] **Tạo và viết `docs/03-design.md`** với ba phần:
  - **A. Kiến trúc** — sơ đồ tổng thể, cấu trúc thư mục, chỗ chạy việc nền, sequence "user bấm Run step N".
  - **B. Data model** — layout thư mục `data/`, schema `project.json`, cách mô hình hoá tiến độ, atomic write, lock.
  - **C. API contract** — danh sách endpoint, request/response, status code (đặc biệt: khi nào trả `409`).
- [x] Ghi các quyết định vào `DECISIONS.md`.

**Decision phải chốt ở phase này:**
[x] 1. Layering: gộp trong `Program.cs` hay tách `Api / Application / Infrastructure`?
[x] 2. Layout file JSON trên disk — phẳng theo loại, hay lồng theo user?
[x] 3. Mô hình hoá tiến độ: `completedSteps` (int) + `runningStep` hay mảng trạng thái 5 phần tử?
[x] 4. Chống ghi đè đồng thời bằng cách nào?
[x] 5. Chạy bước dài 10–30s: `Task.Run` fire-and-forget hay `BackgroundService` + hàng đợi?
[x] 6. Nhận diện user: header `X-User-Email` hay cookie phiên?

**DoD:** vẽ được sequence "user bấm Run step 2" từ click → HTTP → file trên disk → polling → UI, không thiếu mắt xích nào.

**Commit:** `docs: architecture, data model and api contract`

---

## Phase 2 — Walking skeleton

**Mục tiêu:** một đường dây mỏng nhất chạy được đầu-cuối. Chưa có Gemini, chưa có pipeline.

**Cần học:** `dotnet new`, cấu trúc solution; Vite + React; CORS / dev proxy.

**Việc cần làm:**
- [x] `dotnet new web` → project `server`, chạy được `GET /api/health`.
- [x] `npm create vite` → project `client` (React + JavaScript), gọi được `/api/health` và in ra màn hình.
- [x] Xử lý CORS hoặc proxy trong `vite.config.js`.
- [x] Cập nhật `.gitignore`, tạo `.env.example`.
- [x] `start.sh` chạy cả hai bằng một lệnh.

**DoD:** `./start.sh` → mở trình duyệt → thấy chữ đến từ backend.

**Commit:** `chore: scaffold server and client` · `feat: health endpoint wired end to end`

---

## Phase 3 — Storage layer (JSON trên disk)

**Mục tiêu:** đọc/ghi JSON an toàn, thay thế được vai trò của database.

**Cần học:**
- `System.Text.Json`: serialize/deserialize, `JsonSerializerOptions`, đặt tên camelCase.
- **Atomic write**: ghi ra file tạm rồi `File.Move(overwrite: true)` — tại sao ghi đè trực tiếp là nguy hiểm.
- `SemaphoreSlim` và `ConcurrentDictionary` — khoá theo từng project.
- Vì sao `lock` (Monitor) **không dùng được** với `async/await`.

**Việc cần làm:**
- [x] Dựng thư mục `data/` theo phần B của `docs/03-design.md`.
- [x] `JsonStore`: `ReadAsync<T>(path)`, `WriteAsync<T>(path, value)` — ghi atomic.
- [x] `ProjectStore`: `GetAsync(id)`, `ListByUserAsync(email)`, `SaveAsync(project)`, và quan trọng nhất `UpdateAsync(id, Func<Project, bool> mutate)` — đọc-sửa-ghi **bên trong lock**.
- [ ] Test: hai lời gọi `UpdateAsync` song song không được làm mất update của nhau.

**Decision:** khóa theo project (DECISIONS.md ##6). Path dùng userKey (slug+hash) (DECISIONS ##2.2)

**DoD:** viết được một test chạy 50 update song song lên cùng một project, kết quả cuối vẫn đúng.

**Commit:** `feat: json file store with atomic writes` · `feat: per-project write lock` · `test: concurrent updates keep every write`

---

## Phase 4 — Identity & Projects

**Mục tiêu:** đăng nhập bằng email + tên, tạo/liệt kê/xem project.

**Cần học:** Minimal API binding, `Results.*`, validation thủ công, đọc file upload `.txt`.

**Việc cần làm:**
- [ ] `POST /api/auth` — email tồn tại thì trả về, chưa có thì tạo.
- [ ] `POST /api/projects`, `GET /api/projects`, `GET /api/projects/{id}`.
- [ ] Nhận diện user theo quyết định ở Phase 1 — ghi rõ đây **không phải** cơ chế bảo mật thật.
- [ ] Chặn user A đọc project của user B.

**DoD:** dùng curl/Postman tạo được project, restart server, vẫn liệt kê được đủ.

**Commit:** `feat: identity by email` · `feat: project create and list`

---

## Phase 5 — Tầng Gemini + fake client

> Làm fake **trước** khi làm thật (tránh đốt token)

**Cần học:** `HttpClient` / `IHttpClientFactory`, typed client; đọc REST doc của Gemini; structured output bằng JSON schema; nối ngữ cảnh giữa các lượt gọi.

**Việc cần làm:**
- [ ] `IGeminiClient` với 2 method: sinh JSON có schema, sinh ảnh.
- [ ] `FakeGeminiClient` đọc từ `fixtures/*.json`.
- [ ] `GeminiClient` thật, gọi REST, đọc API key từ biến môi trường.
- [ ] Cờ `USE_FAKE_GEMINI` để chọn implementation lúc đăng ký DI.
- [ ] Gọi thật **một lần** cho mỗi loại, lưu response làm fixture.

**Decision:** interface trả về kiểu riêng của mình (`GeminiJsonResult`) hay kiểu HTTP thô? (Kiểu riêng — nếu không, fake phải giả lập cả tầng HTTP.)

**DoD:** với `USE_FAKE_GEMINI=true`, toàn bộ app chạy được mà không cần API key.

**Commit:** `feat: gemini client interface and fake implementation` · `feat: real gemini rest client`

---

## Phase 6 — Pipeline: 2 bước text

**Cần học:** state machine; vì sao "check rồi mới write" là race condition nếu không có lock.

**Việc cần làm:**
- [ ] `PipelineService.RunStepAsync(projectId, step)`.
- [ ] Bước 1 Style, bước 2 Characters (cap **2 nhân vật**, ép ở server).
- [ ] Endpoint `POST /api/projects/{id}/steps/{step}/run` → trả `202 Accepted` ngay, chạy nền.
- [ ] Claim bước: chỉ được chạy step N khi `completedSteps == N-1` và không có step nào đang chạy — kiểm tra **bên trong lock**.
- [ ] Lỗi → lưu `lastError` + `failedStep`, project vẫn dùng được.

**DoD:** double-click nút Run chỉ tạo đúng **một** lời gọi Gemini. Chứng minh bằng test.

**Commit:** `feat: run style step` · `feat: characters step with server-side cap` · `test: duplicate run requests are rejected`

---

## Phase 7 — Pipeline: 3 bước còn lại + ảnh

**Cần học:** lưu và phục vụ file nhị phân; base64; `Results.File`.

**Việc cần làm:**
- [ ] Bước 3 Portraits, bước 4 Chapters (cap **1 chương**), bước 5 Illustrations.
- [ ] Lưu ảnh vào `data/images/{projectId}/`, metadata vào JSON.
- [ ] `GET /api/images/{id}` phục vụ ảnh — **chống path traversal**.
- [ ] Lưu ảnh **ngay sau mỗi tấm**, không đợi cả bước xong (để UI thấy từng tấm rơi xuống).

**Cạm bẫy đã biết từ bản cũ:** bước sinh ảnh có thể trả nhiều hơn một ảnh, và nối ngữ cảnh từ một lượt-ảnh sang một lượt-JSON-schema sẽ bị lỗi. Xử lý ra sao → ghi vào `DECISIONS.md`.

**DoD:** chạy hết 5 bước với fake client, `data/` có đúng số file mong đợi.

**Commit:** `feat: portrait generation` · `feat: chapter and illustration steps`

---

## Phase 8 — Resume, duplicate, stuck

**Mục tiêu:** tính đúng đắn khi có gián đoạn — phần khó nhất về mặt logic của cả dự án.

**Việc cần làm:**
- [ ] Restart server giữa lúc chạy → mở lại project phải thấy đúng trạng thái, không mất kết quả cũ.
- [ ] Bước bị treo (`running` quá lâu) → có đường thoát cho user, không cần sửa file bằng tay.
- [ ] Retry chỉ đúng bước lỗi, không đụng bước đã xong.
- [ ] Không bao giờ auto-retry vòng lặp (đốt tiền).

**Decision:** ngưỡng "treo" là bao nhiêu phút? Ai phát hiện — server khi đọc, hay user bấm nút force?

**DoD:** viết ra được 5 kịch bản hỏng và test tự động cho ít nhất 3 kịch bản.

**Commit:** `feat: stale step recovery` · `test: pipeline resumes after restart`

---

## Phase 9 — Frontend

**Cần học:** `useState` / `useEffect`; polling bằng `setInterval` hoặc library + cleanup; `AbortController`; quản lý state loading/error/empty; điều hướng.

**Việc cần làm:**
- [ ] Màn hình: Identity → Project list → New project → Project detail.
- [ ] Stepper 5 bước: done / current / pending.
- [ ] Polling 2s khi có bước đang chạy, **dừng polling khi không còn gì chạy**.
- [ ] Hiện rõ **tên bước đang chạy**, không phải spinner trống.
- [ ] Empty state, error state + nút retry cho đúng bước đó.
- [ ] Đọc được toàn văn text sách ở mọi thời điểm.

**Cạm bẫy:** `useEffect` không cleanup interval → nhiều interval chồng nhau → gọi API dồn dập.
**Nhớ:** disable nút ở client chỉ là lớp phụ; lớp chống gọi trùng thật là `409` từ server.

**DoD:** tự click qua toàn bộ luồng, kể cả luồng xấu (mạng chậm, bước lỗi, refresh giữa chừng).

**Commit:** mỗi màn hình một commit.

---

## Phase 10 — Test cả hai phía

> Trọng tâm của dự án (`CLAUDE.md §0`) — đầu tư kỹ nhất ở đây, không tính trong deadline 30h của Phase 2-9.

**Cần học:** `WebApplicationFactory` (integration test cho Minimal API); Vitest + React Testing Library; viết test case/test plan/bug report thủ công.

**Tầng 1 — Unit test `PipelineService` (xUnit + Moq).** Tầng quan trọng nhất. Mock `IGeminiClient`; dùng `ProjectStore` **thật** trỏ vào thư mục `data/` tạm, không mock Store (xem lý do ở `DECISIONS.md`). Tối thiểu 5-7 test:
- [ ] Bước đã `Completed` thì không gọi lại — `mockGemini.Verify(x => x.GenerateStyle(...), Times.Never())`.
- [ ] Resume đúng chỗ — bước 1-2 xong, bước 3 failed → assert gọi đúng bước 3.
- [ ] Resume theo item — 3 nhân vật, 2 portrait đã xong → assert đúng 1 lần gọi sinh ảnh cho nhân vật còn thiếu (dựa vào `images[]`, xem `docs/03-design.md` B3).
- [ ] Lỗi được ghi lại chứ không ném ra — mock throw → assert `failedStep`/`lastError` đúng, kết quả bước trước vẫn nguyên.
- [ ] Chặn chạy trùng — bước đang `Running` → request thứ hai bị từ chối.

**Tầng 2 — Integration test bằng `WebApplicationFactory`,** inject `FakeGeminiClient`, gọi endpoint thật. 3-4 test: tạo project, chạy pipeline, đọc status.

**Tầng 3 — FE test bằng Vitest + React Testing Library,** 3-4 test vào chỗ có logic: stepper render đúng status, form tạo project chặn text rỗng, polling dừng khi pipeline xong.

**E2E/Playwright:** không làm ở bản đầu — ghi ở `README.md` như nice-to-have, chưa quyết định làm.

**Test case thủ công (bắt buộc cho mục tiêu CV Tester):**
- [ ] Viết test plan: phạm vi, chiến lược, môi trường test.
- [ ] Viết test case cho luồng chính + luồng lỗi (bảng: ID, bước, input, expected, actual, pass/fail).
- [ ] Chạy tay ít nhất 1 lượt đầy đủ, ghi bug report mẫu nếu tìm thấy lỗi (kể cả lỗi cố ý tạo ra để có mẫu thật).

**Việc cần làm khác:**
- [ ] `test.sh` chạy cả hai bằng một lệnh.
- [ ] **Tạo và viết `TESTING.md`**: test gì, **cố ý không test gì và tại sao**, kèm output của một lần chạy thật.

**DoD:** `./test.sh` xanh trên máy sạch; có bộ test case thủ công đọc được, không chỉ code test.

**Commit:** `test: backend pipeline suite` · `test: frontend component states` · `docs: testing strategy` · `docs: manual test plan and case`

---

## Phase 11 — Hoàn thiện & tổng kết

**Việc cần làm:**
- [ ] **Tạo và viết `README.md`**: prerequisites, 1 lệnh chạy, 1 lệnh test, biến môi trường, sơ đồ kiến trúc ngắn.
- [ ] `DECISIONS.md`: hoàn chỉnh, mỗi quyết định có **cái giá đã chấp nhận**.
- [ ] `docs/learning-log.md`: đọc lại toàn bộ, tổng kết.
- [ ] So sánh với bản cũ (SQLite + TypeScript): chỗ nào bản mới dễ hơn, chỗ nào khó hơn, tại sao.
- [ ] Tự trả lời: "nếu có thêm một ngày, tôi sẽ làm gì tiếp và vì sao?"

**DoD:** giải thích được toàn bộ dự án cho người khác trong 15 phút mà không mở code.

