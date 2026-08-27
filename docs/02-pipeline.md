# 02 — Pipeline & Gemini

> **Điền file này SAU KHI tự chạy notebook trên Colab.** Không viết theo trí nhớ, không đoán.
> Notebook: *Illustrate a book: The Wind in the Willows* — chỉ làm bước 1–5.
> Đây là file sẽ được bổ sung dần: mỗi lần phát hiện thêm một hành vi của API, ghi vào đây.

---

## 1. Bảng tổng quan 5 bước

| # | Bước | Input | Output | Cap |
|---|---|---|---|---|
| 1 | Style | text sách (hoặc style user nhập) | một mô tả phong cách nhất quán | — |
| 2 | Characters | ngữ cảnh từ bước 1 | danh sách nhân vật, mỗi người có `imagePrompt` | **≤ 2** |
| 3 | Portraits | prompt của từng nhân vật | 1 ảnh / nhân vật | |
| 4 | Chapters | ngữ cảnh (đã biết style + nhân vật) | danh sách prompt minh hoạ chương | **≤ 1** |
| 5 | Illustrations | prompt chương + portrait | 1 ảnh / chương, nhân vật nhất quán | |

**Cap ép ở server:** sau khi parse JSON từ model, cắt bớt — vì model **sẽ** trả nhiều hơn nếu sách có nhiều nhân vật.
Notebook giới hạn "chỉ nhân vật người lớn" — giữ nguyên, lý do an toàn nội dung khi sinh ảnh người.

## 2. Model dùng cho từng bước

> Model ID thay đổi theo thời gian — tra doc, đừng chép từ đâu khác.

| Việc | Model | Ghi chú |
|---|---|---|
| Sinh text / JSON | ☐ … | |
| Sinh ảnh | ☐ … | kiểm tra free-tier limit **trước khi** bắt đầu, thường chặt hơn model text |

## 3. Gửi text sách một lần (FR-29)

Ba cách để bước 2–5 "nhớ" nội dung sách mà không gửi lại:

| Cách | Cơ chế | Ghi chú |
|---|---|---|
| A. Chaining hội thoại | mỗi lượt gọi tham chiếu id của lượt trước | phải lưu id đó xuống disk |
| B. File upload + URI | upload sách một lần, các bước sau trỏ tới URI | cần biết TTL |
| C. Context caching | cache prefix ngữ cảnh | thường có ngưỡng token tối thiểu |

**Chốt:** ☐ A ☐ B ☐ C — lý do ghi vào `DECISIONS.md`.

**Điểm nối với FR-23:** con trỏ ngữ cảnh đó **phải được lưu xuống disk cùng project**. Chỉ giữ trong RAM thì restart server là mất → không resume được.
- Con trỏ sống được bao lâu? ☐ …
- Hết hạn thì làm gì? ☐ …

## 4. Structured output (bước 2 và 4)

Cách khai báo response schema trong REST: ☐ …

Schema nháp cho bước 2:

```jsonc
{ "type": "object",
  "properties": { "characters": { "type": "array", "items": {
      "type": "object",
      "properties": { "name": {"type":"string"}, "imagePrompt": {"type":"string"} },
      "required": ["name","imagePrompt"] }}},
  "required": ["characters"] }
```

- Vì sao bọc trong object có key `characters` thay vì trả thẳng mảng? ☐ …
- Dù có schema, **vẫn phải xử lý parse fail**. Parse fail → coi là bước lỗi, user retry (FR-26), **không** auto-retry (FR-28).

## 5. Sinh ảnh (bước 3 và 5)

- Ảnh trả về dạng gì — base64 inline hay URL? ☐ …
- Bước 5 dùng lại portrait để nhân vật nhất quán bằng cách nào? ☐ …

**Hai cạm bẫy đã biết từ bản cũ — xác minh lại rồi ghi kết luận:**

1. Model có thể trả **nhiều hơn một ảnh** cho một lần gọi. Nếu code giả định "một lần gọi = một ảnh" sẽ lấy nhầm.
   → Xử lý: ☐ lấy ảnh cuối ☐ ràng buộc trong prompt ☐ cả hai
2. Nối ngữ cảnh từ một lượt-sinh-ảnh sang một lượt-yêu-cầu-JSON-schema **có thể bị lỗi**. Nghĩa là con trỏ dùng cho các bước text phải luôn trỏ tới lượt-text gần nhất.
   → Kết luận sau khi tự kiểm chứng: ☐ …

## 6. Prompt của từng bước

> Prompt là một phần của spec, không phải chuỗi vứt lung tung trong code. Chép lại từ notebook rồi chỉnh.

**Bước 1 – Style:** ☐ …
**Bước 2 – Characters:** ☐ …
**Bước 3 – Portraits:** ☐ …
**Bước 4 – Chapters:** ☐ …
**Bước 5 – Illustrations:** ☐ …

## 7. Lỗi cần xử lý

| Tình huống | Xử lý ra sao | Đã làm? |
|---|---|---|
| 401 — sai API key | báo rõ lúc khởi động, không phải lúc chạy bước 1 | ☐ |
| 429 — hết quota | báo lỗi bước, user tự retry, **không** auto-retry | ☐ |
| Timeout | đặt timeout cho HttpClient — bao nhiêu? (ảnh mất 30s+) ☐ … | ☐ |
| Response không parse được | coi là bước lỗi | ☐ |
| Model trả 0 nhân vật | ☐ … | ☐ |
| Server chết giữa lúc gọi | rơi vào cơ chế stuck (FR-27) | ☐ |

## 8. Fixture

Gọi thật **một lần** cho mỗi loại → lưu nguyên response vào `fixtures/` → từ đó dev bằng `FakeGeminiClient`.

| Fixture | Nguồn | Đã có? |
|---|---|---|
| `style.json` | bước 1 | ☐ |
| `characters.json` | bước 2 | ☐ |
| `portrait.png` + metadata | bước 3 | ☐ |
| `chapters.json` | bước 4 | ☐ |
| `illustration.png` + metadata | bước 5 | ☐ |
