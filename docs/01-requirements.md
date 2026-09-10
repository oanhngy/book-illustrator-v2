# 01 — Requirements

> Nguồn: `docs/gradion-assessment-intern-software-engineer.md`.
> File này biến đề bài thành **checklist có mã**. Mọi thứ code ra phải truy ngược được về một mã ở đây.
> Priority: **P0** bắt buộc cho MVP (thiếu thì không kể được câu chuyện CV, deadline 30h)
> · **P1** nên có nếu còn giờ, pipeline chính vẫn chạy đúng khi thiếu
> · **P2** có thể bỏ — nếu bỏ, phải ghi lý do vào README/TESTING.md.

---

## 1. Identity

| Mã | Yêu cầu | Priority |
|---|---|---|
| FR-01 | Đăng nhập bằng **email + tên**, không mật khẩu, không OAuth | P0 |
| FR-02 | Email đã tồn tại → nạp project của user đó; chưa có → tạo user mới | P0 |
| FR-03 | Có sign out | P1 |

## 2. Projects

| Mã | Yêu cầu | Priority |
|---|---|---|
| FR-10 | Tạo project từ text sách: **paste** hoặc **upload `.txt`**, kèm tiêu đề | P0 |
| FR-11 | Một user có nhiều project; chỉ thấy project của chính mình | P0 |
| FR-12 | Danh sách project hiển thị: tiêu đề, ngày tạo, trạng thái, tiến độ 5 bước | P0 |
| FR-13 | Mở project → thấy đang ở đâu trong pipeline và chạy được bước kế tiếp | P0 |
| FR-14 | Đọc được **toàn văn** text sách ở bất kỳ thời điểm nào | P1 |

## 3. Pipeline

| Mã | Yêu cầu | Priority |
|---|---|---|
| FR-20 | 5 bước đúng thứ tự: Style → Characters → Portraits → Chapters → Illustrations | P0 |
| FR-21 | **User-driven**: mỗi bước cần một hành động rõ ràng của user | P0 |
| FR-22 | Bước N không chạy được nếu bước N-1 chưa thành công | P0 |
| FR-23 | **Resumable**: refresh / logout / restart server giữa chừng → mở lại thấy đúng trạng thái, không mất kết quả, không chạy lại từ đầu | P0 |
| FR-24 | **No duplicate calls**: refresh, tab thứ hai, double-click → không gọi Gemini hai lần | P0 |
| FR-25 | In-progress phải nói rõ **bước nào** đang chạy, không phải spinner trống | P1 |
| FR-26 | Bước lỗi → project vẫn dùng được, retry **đúng bước đó** | P0 |
| FR-27 | Bước treo ở trạng thái "đang chạy" → user có đường thoát, không sửa file bằng tay | P1 |
| FR-28 | Không auto-retry Gemini trong vòng lặp. Retry chỉ do user bấm | P0 |
| FR-29 | Text sách chỉ gửi cho Gemini **một lần**, các bước sau dùng lại ngữ cảnh | P0 |
| FR-30 | Cap **≤ 2 nhân vật**, **≤ 1 chương** — ép ở **server**, không phải ở UI | P0 |
| FR-31 | Bước 1 nhận style do user nhập (tuỳ chọn); không nhập thì sinh từ text sách | P1 |
| FR-32 | Ảnh hiện **từng tấm** khi sinh xong, không đợi cả bước | P0 |
| FR-33 | Retry giữ nguyên ảnh cũ, chỉ sinh ảnh khi thiếu | P0 |

## 4. Frontend

| Mã | Màn hình / state | Priority |
|---|---|---|
| FR-40 | Identity — có validation | P0 |
| FR-41 | Project list — có empty state | P0 |
| FR-42 | New project — upload `.txt` **và** paste, có validation | P0 |
| FR-43 | Project detail — stepper 5 bước done/current/pending | P0 |
| FR-44 | Style hiện tại hiển thị được | P1 |
| FR-45 | Character card: tên, prompt, portrait | P0 |
| FR-46 | Chapter card: tên, prompt, illustration | P0 |
| FR-47 | Một nút hành động rõ ràng cho bước hiện tại | P0 |
| FR-48 | Error state + nút retry cho đúng bước đó | P0 |
| FR-49 | Có cách khôi phục khi bước bị treo | P1 |

## 5. Phi chức năng

| Mã | Yêu cầu | Priority |
|---|---|---|
| NFR-01 | Storage: **JSON files trên disk**. Tách theo user/project, an toàn khi ghi đồng thời | P0 |
| NFR-02 | Ảnh + text sách nằm trên filesystem local, phục vụ qua API của mình. Không S3/CDN | P0 |
| NFR-03 | Gemini API key qua biến môi trường, **không commit**. Có `.env.example` | P0 |
| NFR-04 | Test cả backend lẫn frontend (automated: unit/integration/FE) và test case thủ công + test plan + bug report mẫu, ghi trong TESTING.md, kèm report một lần chạy thật | P0 |
| NFR-05 | **Một lệnh** chạy stack, **một lệnh** chạy test | P1 |
| NFR-06 | Right-sized: thêm bước thứ 6 không phải viết lại, nhưng không có abstraction cho thứ chưa ship | Nguyên tắc |
| NFR-07 | Git history: commit nhỏ, có nghĩa, rải theo thời gian | Nguyên tắc |
| NFR-08 | Chấp nhận text dài tối đa 500.000 ký tự | P0 |

## 6. Ngoài phạm vi

- Veo (animation), Lyria (nhạc), TTS, trộn media, audiobook
- Deploy công khai
- Auth thật (mật khẩu, OAuth, JWT)
- Chạy nhiều instance server cùng lúc
- Không cho phép re-run bước đã Completed

## 7. MVP bắt buộc (P0) — checklist rút gọn cho deadline 30h
**Identity:** FR-01, FR-02
**Projects:** FR-10, FR-11, FR-12, FR-13
**Pipeline:** FR-20, FR-21, FR-22, FR-23, FR-24, FR-26, FR-28, FR-29, FR-30, FR-32, FR-33
**Frontend:** FR-40, FR-41, FR-42, FR-43, FR-45, FR-46, FR-47, FR-48
**Phi chức năng:** NFR-01, NFR-02, NFR-03, NFR-04, NFR-08
