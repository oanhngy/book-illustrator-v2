
## 0. Bảng tổng quan 5 bước

| # | Bước | Input | Output | Cap |
|---|---|---|---|---|
| 1 | Style | text sách (hoặc style user nhập) | một mô tả phong cách nhất quán | — |
| 2 | Characters | ngữ cảnh từ bước 1 | danh sách nhân vật, mỗi người có `imagePrompt` | **≤ 2** |
| 3 | Portraits | prompt của từng nhân vật | 1 ảnh / nhân vật | |
| 4 | Chapters | ngữ cảnh (đã biết style + nhân vật) | danh sách prompt minh hoạ chương | **≤ 1** |
| 5 | Illustrations | prompt chương + portrait | 1 ảnh / chương, nhân vật nhất quán | |

---

## 1. How pipeline (1-5) work in Colab
each step have belowed content:
- gọi endpoint (url) nào
- gửi + nhận gì
- ngữ cảnh nối sang bước sau bằng cơ chế nào
- CHUNG cap=capacity=context window, Genimi 1-2tr, sách đưa vào khoảng 100k token(10%)
- prompt từng bước

**1. Get a book and upload using the File API**
- 2 endpoint: (1) requests.get(url)= gọi lên server chứa sách-->tải về project); (2) client.files.upload(file="book.txt)=gọi Gemini File, upload sách mới tải về lên server google để gemini đọc đc
- gửi: all nội dung trong book.txt
- nhận: object gán vô biến **book**, object chứa metadata, quan trọng nhất là uri, = cách xem uri --> Gemini tự biết đường tìm lại file
- nối sang bước sau=biến **book** (chứa uri) và biến **system.instructions**, bước sau truyền book vô hàm tạo prompt
- system_instructions có role như bộ quy tắc chung, tạo 1 lần xài luôn. Ở bước này mới đc khai báo như 1 biến string --> Bước sau (usually hàm GenerativeModel) biến này mới đc truyền vào

**2. Start the chat**
- 1 endpoint: client.interactions.create
- gửi: model, input là list có 2 object (type text và type document)
- nhận: object lưu vào biến book_interaction (chứa info lượt chat vừa qua)
- nối sang bước sau=IDs (cơ chế API interactions)
- prompt: trong input gửi đi "Here's a book..."

**3. Define a style**
- 1 endpoint: client.interactions.create
- gửi: các tham số (arguments) trong hàm client.interactions.create() (cả if else) gồm model, input, previous_interaction_id; biến style user nhập
- nhận: object chứa thông tin lượt chat mới, lưu về style_interaction (cho if else), sau đó gán last_interaction=style_interaction cho tiện gọi trong bước sau
- nối sang bước sau=biến last_interaction, biến style
- prompt: trong if "Can you define a art style...", else "The art style will be:{style}". Keep...", f'Follow this style:"{style}" '

**4.Generate portraits of the main characters**
**4.1 text generate describe each of the main characters**
- endpoint: client.interactions.create()
- send: tham số bên trong hàm trên, chứa model, input(prompt), previous_interaction_id, response_format
- receive: nội dung trong biến last_interaction=characters_prompt_interactions là nội dung của cuộc trò chuyện trên
- nối bước sau=last_interaction(cụ thể là last_interaction.id)
- prompt: là input="Can you describe..."

**4.2 portrait image generate**
- endpoint: client.interactions.create() loop qua từng characters
- send: tham số bên trong hàm trên chứa biến model, input(prompt), previous_interaction_id
- receive: generate_image chứa ảnh Gemini đã tạo (nếu có ảnh được tạo), biến last_image_interaction=characters_image_interaction chứa lịch sử chat
- nối bước sau=last_image_interaction
- prompt: nằm trong hàm client.interactions.create

**5. Illustrate the chapters of the book**
**5.1 generate prompts**
- endpoint: client.interaction.create()
- gửi: tham số bên trong hàm trên gồm model, input, previous_interaction_id(của 4.1, nhớ toàn bộ sách+ngoại hình character), response_format
- nhận: dlieu JSON lưu vô biến last_interacion-chapters_prompts_interaction chứa lịch sử chat
- nối sang bước sau= last_interaction
- prompt: bên trong hàm client.interaction.create()

**5.2 generate image from prompt above**
- endpoint: client.interaction.create()
- gửi: tham số bên trong hàm trên có previous_interaction_id( từ 4.2, ensure nối tiếp cái stype và system_instructions từ trước)
- nhận: last_image_interaction=chapters_image_interaction, ảnh Gemini generate
- nối sang bước sau= last_image_interaction
- prompt: trong tham số input của hàm trên


## 2. Gửi text sách một lần (FR-29)
**Chốt:** Context chaining qua ID, phân tích các cách chaining/ file upload + url/ chainign hội thoại — lý do ghi vào `DECISIONS.md`.

**| FR-23 | Resumable: refresh / logout / restart server giữa chừng → mở lại thấy đúng trạng thái, không mất kết quả, không chạy lại từ đầu**
(*) Trong Colab, "con trỏ ngữ cảnh" là book.uri, last_interaction.id cho text và last_image_interaction.id cho hình. Các ID đc lưu=biến Python, nếu chạy lại -->mất hết. "Con trỏ" cần lưu xuống disk, không chỉ giữ trong RAM (restart server mất) --> not resumable
- Con trỏ sống được bao lâu (TTL - Time To Live)?
   - File API (book.uri) default=48h
   - Interactions (last_interaction.id) phụ thuộc vòng đời tài nguyên trên server gg, same as book.uri
- Hết hạn thì làm gì? khi hết hạn, vẫn gọi thì trả 404 hoạc invalid. Thực hiện:
   - Try-catch
   - Re-construct Context (khôi phục trạng thái ngầm): (1) tự động upload lại file book.txt lên gg lấy book.uri mới và (2) tạo session mới
   - tái sdung dữ liệu đã có: mất id cũ nhưng các data như style, characters... đã lưu thành công --> k bắt AI tạo lại, only prompt thiết lập bối cảnh + tiếp tục bước dang dở

**Solution FR-23**: có Id mới, cần lưu vào project.json kèm user_id/project_id. Khi F5, BE only query lấy ra ID cưới cùng, nhét vào previous_interaction_id r gọi API tiếp

## 3. Structured output (bước 2 và 4)

3.1. Vì sao bọc trong object có key `characters` thay vì trả thẳng mảng? Bọc trong 1 object như Characters là best practice vì:
   - Extensibility: xài object thì thêm bớt chỉ cần thêm key, code cũ k gãy. Nếu trả thảng array muốn sửa đổi phải xây lại toàn bộ
   - Anchoring: LLM=đoán next words, khi mớm {} và 1 key rõ ràng như characters, AI hiểu ngay ngữ cảnh, ít hallucinate hơn
   - Tương thích vs Pydantic(hoặc các thư viện Validation): Trong Python nhay các thư viện định nghĩa Schema luôn định nghĩa Class tương đương 1 object --> Ánh xạ từ 1 JSONObject sang 1 python object là tự nhiên + ít lỗi

**| FR-26 | Bước lỗi → project vẫn dùng được, retry **đúng bước đó**** --> system stop, hiển thị button Retry + nội dung lỗi
**| FR-28 | Không auto-retry Gemini trong vòng lặp. Retry chỉ do user bấm** -->auto-retry=đốt tiền
3.2. Dù có schema, **vẫn phải xử lý parse fail**. Parse fail → coi là bước lỗi, user retry (FR-26), **không** auto-retry
--> bắt buộc defense conding = try catch/try except


## 4. Sinh ảnh (bước 3 và 5)

- Ảnh trả về dạng gì? raw bytes và mã hóa thành base64 inline
- Bước 5 dùng lại portrait để nhân vật nhất quán bằng cách nào? Kết hợp Kiến trúc Luồng kép(Dual-Thread):
   - Text: 5.1 nối previous_interaction_id của 4.1 --> Gemini nhớ chính xác mô tả=text của characters đã chốt ở 4.1
   - Image: 5.2 tiếp previous_interaction_id (last_image_interaction.id) của 4.2

**Cạm bẫy đã biết từ bản cũ — xác minh lại rồi ghi kết luận:**

Model có thể trả **nhiều hơn một ảnh** cho một lần gọi. Nếu code giả định "một lần gọi = một ảnh" sẽ lấy nhầm.
--> Xử lý: lấy ảnh cuối + ràng buộc trong prompt

## 5. Prompt của từng bước

> copy paste nội dung prompt, note vị trí
**system_instructions**
There must be no text on the image, it should not look like a cover page. It should be an full illustration with no borders, titles, nor description. Unless asked otherwise, stay family-friendly with uplifting colors. Each produced should be a simple image, no panels.

**Bước 1 – Style:**
IF: Can you define a art style that would fit the story but with a twist? Just give us the prompt for the art syle that will added to the furture prompts.
ELSE: The art style will be:"{style}". Keep that in mind when generating future prompts. Keep quiet for now, instructions will follow.

**Bước 2 – Characters:**
Can you describe the main characters (only the adults) and prepare a prompt describing them with as much details as possible (use the descriptions from the book) so Nano Banana can generate images of them? Each prompt should be at least 50 words.

**Bước 3 – Portraits:**
You are going to generate portrait images to illustrate name_of_book. The style we want you to follow is: {style}. Also follow those rules: {system_instructions} # TODO: Sysyem instructions

**Bước 4 – Chapters:**
Now, for each chapters of the book, give me a prompt to illustrate what happens in it. It should be a single image, not a multi-tiled page. Be very descriptive, especially of the characters. Be very descriptive and remember to tell their name and to reuse the character prompts if they appear in the images. Also list all characters who appear in it.

**Bước 5 – Illustrations:**
Starting from now, we're going to illustrate the book's chapters. Don't forget to refer to your previous illustrations of the characters to keep the characters consistency, but feel free to change their position.
