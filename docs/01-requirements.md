# 01 — Requirements

> Nguồn: `docs/gradion-assessment-intern-software-engineer.md`.
> File này biến đề bài thành **checklist có mã**. Mọi thứ code ra phải truy ngược được về một mã ở đây.
> Status: ☐ chưa làm · ◐ đang làm · ☑ xong và đã tự kiểm chứng.

---

## 1. Identity

| Mã | Yêu cầu | Status |
|---|---|---|
| FR-01 | Đăng nhập bằng **email + tên**, không mật khẩu, không OAuth | ☐ |
| FR-02 | Email đã tồn tại → nạp project của user đó; chưa có → tạo user mới | ☐ |
| FR-03 | Có sign out | ☐ |

## 2. Projects

| Mã | Yêu cầu | Status |
|---|---|---|
| FR-10 | Tạo project từ text sách: **paste** hoặc **upload `.txt`**, kèm tiêu đề | ☐ |
| FR-11 | Một user có nhiều project; chỉ thấy project của chính mình | ☐ |
| FR-12 | Danh sách project hiển thị: tiêu đề, ngày tạo, trạng thái, tiến độ 5 bước | ☐ |
| FR-13 | Mở project → thấy đang ở đâu trong pipeline và chạy được bước kế tiếp | ☐ |
| FR-14 | Đọc được **toàn văn** text sách ở bất kỳ thời điểm nào | ☐ |

## 3. Pipeline

| Mã | Yêu cầu | Status |
|---|---|---|
| FR-20 | 5 bước đúng thứ tự: Style → Characters → Portraits → Chapters → Illustrations | ☐ |
| FR-21 | **User-driven**: mỗi bước cần một hành động rõ ràng của user | ☐ |
| FR-22 | Bước N không chạy được nếu bước N-1 chưa thành công | ☐ |
| FR-23 | **Resumable**: refresh / logout / restart server giữa chừng → mở lại thấy đúng trạng thái, không mất kết quả, không chạy lại từ đầu | ☐ |
| FR-24 | **No duplicate calls**: refresh, tab thứ hai, double-click → không gọi Gemini hai lần | ☐ |
| FR-25 | In-progress phải nói rõ **bước nào** đang chạy, không phải spinner trống | ☐ |
| FR-26 | Bước lỗi → project vẫn dùng được, retry **đúng bước đó** | ☐ |
| FR-27 | Bước treo ở trạng thái "đang chạy" → user có đường thoát, không sửa file bằng tay | ☐ |
| FR-28 | Không auto-retry Gemini trong vòng lặp. Retry chỉ do user bấm | ☐ |
| FR-29 | Text sách chỉ gửi cho Gemini **một lần**, các bước sau dùng lại ngữ cảnh | ☐ |
| FR-30 | Cap **≤ 2 nhân vật**, **≤ 1 chương** — ép ở **server**, không phải ở UI | ☐ |
| FR-31 | Bước 1 nhận style do user nhập (tuỳ chọn); không nhập thì sinh từ text sách | ☐ |
| FR-32 | Ảnh hiện **từng tấm** khi sinh xong, không đợi cả bước | ☐ |

## 4. Frontend

| Mã | Màn hình / state | Status |
|---|---|---|
| FR-40 | Identity — có validation | ☐ |
| FR-41 | Project list — có empty state | ☐ |
| FR-42 | New project — upload `.txt` **và** paste, có validation | ☐ |
| FR-43 | Project detail — stepper 5 bước done/current/pending | ☐ |
| FR-44 | Style hiện tại hiển thị được | ☐ |
| FR-45 | Character card: tên, prompt, portrait | ☐ |
| FR-46 | Chapter card: tên, prompt, illustration | ☐ |
| FR-47 | Một nút hành động rõ ràng cho bước hiện tại | ☐ |
| FR-48 | Error state + nút retry cho đúng bước đó | ☐ |
| FR-49 | Có cách khôi phục khi bước bị treo | ☐ |

## 5. Phi chức năng

| Mã | Yêu cầu | Status |
|---|---|---|
| NFR-01 | Storage: **JSON files trên disk**. Tách theo user/project, an toàn khi ghi đồng thời | ☐ |
| NFR-02 | Ảnh + text sách nằm trên filesystem local, phục vụ qua API của mình. Không S3/CDN | ☐ |
| NFR-03 | Gemini API key qua biến môi trường, **không commit**. Có `.env.example` | ☐ |
| NFR-04 | Test cả backend lẫn frontend + `TESTING.md` + report một lần chạy thật | ☐ |
| NFR-05 | **Một lệnh** chạy stack, **một lệnh** chạy test | ☐ |
| NFR-06 | Right-sized: thêm bước thứ 6 không phải viết lại, nhưng không có abstraction cho thứ chưa ship | ☐ |
| NFR-07 | Git history: commit nhỏ, có nghĩa, rải theo thời gian | ☐ |

## 6. Ngoài phạm vi

- Veo (animation), Lyria (nhạc), TTS, trộn media, audiobook.
- Deploy công khai.
- Auth thật (mật khẩu, OAuth, JWT).
- Chạy nhiều instance server cùng lúc.

## 7. Câu hỏi mở

> Điền khi đọc đề và chạy notebook. Trả lời được rồi thì gạch đi, hoặc chuyển thành một FR mới.

- [ ] User sửa style rồi chạy lại bước 1 khi đã tới bước 4 — cho phép không? Nếu cho thì kết quả bước 2–4 xử lý ra sao?
- [ ] Text sách dài tối đa bao nhiêu thì chấp nhận?
- [ ] Retry bước sinh ảnh — ảnh cũ bị xoá hay giữ lại?
- [ ] …
