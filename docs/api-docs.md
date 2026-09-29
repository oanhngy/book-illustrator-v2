## 0. Quy ước chung
- Base URL: `https://generativelanguage.googleapis.com`
- Auth: header `x-goog-api-key: {GEMINI_API_KEY}` cho mọi request. **Không dùng `?key=`** vì key lọt vào URL và log. Key đọc từ biến môi trường `GEMINI_API_KEY`.
- Chat/JSON/ảnh đều dùng **Interactions API**: `POST /v1beta/interactions` (GA từ 06/2026). Không dùng `generateContent` hay `:predict`.
- Nối ngữ cảnh: response trả `id` → lượt sau gửi `previous_interaction_id` = id đó. `store` mặc định `true`, **không tắt** (tắt thì không chain được).
- `system_instruction` chỉ áp cho **từng lượt**, phải gửi lại ở mọi request. `GeminiClient` giữ giá trị này từ cấu hình khởi tạo và tự đính vào mỗi request, không nằm trong request DTO (DECISIONS ##14).
- Thời hạn: file upload 48 giờ; interaction lưu 1 ngày (gói free) --> hết hạn gọi lại lỗi, cần dựng lại ngữ cảnh (Phase 8)

## 1. UploadBookAsync — Files API, luồng resumable 2 bước

### Bước 1: mở phiên upload
- `POST /upload/v1beta/files`
- Header:
| Header | Giá trị |
|---|---|
| x-goog-api-key | {GEMINI_API_KEY} |
| X-Goog-Upload-Protocol | resumable |
| X-Goog-Upload-Command | start |
| X-Goog-Upload-Header-Content-Length | số byte UTF-8 của book (dạng chuỗi) |
| X-Goog-Upload-Header-Content-Type | text/plain |
| Content-Type | application/json |
- Body: `{"file":{"display_name":"book.txt"}}`
- Response: **URL để upload nằm ở response header `X-Goog-Upload-URL`**, không nằm trong body.

### Bước 2: gửi nội dung
- `POST {X-Goog-Upload-URL}` (URL lấy từ bước 1)
- Header:
| Header | Giá trị |
|---|---|
| X-Goog-Upload-Offset | 0 |
| X-Goog-Upload-Command | upload, finalize |
| Content-Length | số byte (khớp bước 1) |
- Body: nội dung sách dạng raw bytes (`StringContent` với UTF-8), không bọc JSON.
- Response 200:
```json
{ "file": { "name": "files/abc123", "mimeType": "text/plain", "uri": "https://generativelanguage.googleapis.com/v1beta/files/abc123", "state": "ACTIVE", "expirationTime": "..." } }
```
- Lấy `file.uri` (string) → `Project.BookUri`. Nếu `state` khác `ACTIVE` thì coi là lỗi. **chưa rõ file text có bao giờ ở `PROCESSING` không-->cần test**
- Lưu ý C#: độ dài phải là **số byte** (`Encoding.UTF8.GetByteCount`), không phải `string.Length`. Sách tiếng Việt/ký tự đặc biệt sẽ lệch.

## 2. GenerateJsonAsync — structured output
- `POST /v1beta/interactions`
- Header: `Content-Type: application/json`, `x-goog-api-key`
- Body:
```json
{
  "model": "{TEXT_MODEL}",
  "system_instruction": "{system_instructions}",
  "previous_interaction_id": "{id lượt trước, bỏ field này nếu là lượt đầu}",
  "input": [
    { "type": "document", "uri": "{Project.BookUri}", "mime_type": "text/plain" },
    { "type": "text", "text": "{prompt}" }
  ],
  "response_format": {
    "type": "text",
    "mime_type": "application/json",
    "schema": {
      "type": "object",
      "properties": {
        "characters": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": { "name": {"type":"string"}, "imagePrompt": {"type":"string"} },
            "required": ["name", "imagePrompt"]
          }
        }
      },
      "required": ["characters"]
    }
  }
}
```
- Schema là **JSON Schema kiểu chữ thường** (`"object"`, `"string"`), không phải kiểu OBJECT viết hoa của `generateContent`.
- Item `document` (sách) **chỉ gửi ở lượt đầu tiên** (`previous_interaction_id` rỗng, tức bước Style). Các lượt sau server đã nhớ qua chain. ⚠ chưa xác minh loại `document` + `uri` cho file `.txt` (ví dụ chính thức chỉ có `type:"image"` + `uri`).
- `{TEXT_MODEL}`: `gemini-3.1-flash-lite`

### Response 200
```json
{
  "id": "v1_ChdPU0F4...",
  "status": "completed",
  "steps": [
    { "type": "model_output", "content": [ { "type": "text", "text": "{\"characters\":[...]}" } ] }
  ]
}
```
- `id` → `GeminiJsonResult.InteractionId`.
- Text JSON: lấy item `type:"text"` của step `model_output` trong `steps` (`steps[].content[].text`) → `JsonDocument.Parse` → `GeminiJsonResult.Data`. `output_text` chỉ là helper của SDK, không phải field REST--> đối chiếu bằng 1 lần gọi thật
- `status` khác `completed` → throw. Parse JSON lỗi → throw (bước lỗi, user retry, không auto-retry).

## 3. GenerateImageAsync
- `POST /v1beta/interactions` (cùng endpoint)
- Model: `gemini-3.1-flash-lite-image`
- Body:
```json
{
  "model": "gemini-3.1-flash-lite-image",
  "system_instruction": "{system_instructions}",
  "previous_interaction_id": "{id lượt trước}",
  "input": [ { "type": "text", "text": "{prompt}" } ],
  "response_format": { "type": "image", "mime_type": "image/png", "aspect_ratio": "1:1" }
}
```
- Lượt ảnh đầu tiên (Portrait #1) `previous_interaction_id` = id của lượt Characters (text). Các lượt sau = id lượt ảnh trước (xem docs/02-pipeline.md, mục Dual-Thread).
- `aspect_ratio`: giá trị hợp lệ xem trang image-generation

### Response 200
```json
{
  "id": "v1_...",
  "status": "completed",
  "steps": [
    { "type": "model_output", "content": [
      { "type": "text", "text": "..." },
      { "type": "image", "mime_type": "image/png", "data": "iVBORw0KGgo..." }
    ] }
  ]
}
```
- Response có thể chứa **cả text lẫn nhiều ảnh**. Duyệt các item, **lấy item `type:"image"` cuối cùng** (DECISIONS ##15). Không có ảnh nào → throw.
- `data` → `Convert.FromBase64String` → `GeminiImageResult.ImageBytes`; `mime_type` → `MimeType`; `id` → `InteractionId`.

## 4. Lỗi HTTP
- Status khác 2xx → throw exception kèm status code và body. Phase 6 bắt và ghi `lastError`.
- Không auto-retry (FR-28).
