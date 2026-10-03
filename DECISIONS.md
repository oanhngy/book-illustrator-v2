## DECISIONS
## 1. Cấu trúc dự án - Layering
**Người đề xuất** PLAN.md
**Bối cảnh** NFR-06 Right-sizing. Dự án scope nhỏ, domain logic đơn giản
**Options**
| Cách | Được | Mất | Áp dụng |
| --- | --- | --- | --- |
| A. Tách project Api/App/Infras | Chia file rõ, ranh giới do compiler ép | Boilerplate, nặng cho ~ 15 endpoints | Domain phức tạp, team lớn |
| B. Một project chia thư mục | Gọn, dễ nhìn toàn cảnh | Không có ranh giới cứng, nguy cơ dependency chảy ngược | Scope nhỏ, 1 người |
**Chốt** B
**Trade-offs**
    - Nguy cơ Circular dependency --> solve=Single Responsibility: Storage/ không được biết khái niệm "step/run/pipeline". Signature các hàm trong ProjectStore chỉ nói ngôn ngữ CRUD (Get, Save, Update(mutate)), không nói ngôn ngữ nghiệp vụ pipeline, ngoài ra grep/NetArchTest (cơ chế phát hiện) thực hiện ở các Phase cuối (khi có đủ kiến trúc)
    - Tránh Program.cs lộn xộn khi nhiều endpoint bằng Extension Method
**Xem lại khi** khi domain logic pipeline phình to (nhiều step phức tạp cần cô lập)

---
## 2. Data model - Layout thư mục
## 2.1 Mô hình=Data model phẳng
**Người đề xuất** PLAN.md
**Bối cảnh** ảnh hưởng trực tiếp ProjectStore/JsonStore 
**Options**
| Vấn đề | Phẳng | Lồng theo User |
| --- | --- | --- |
| list all project của 1 user | mỗi loại data có thự mục riêng, cần quét toàn bộ projects/ | mỗi user có folder riêng, tìm project theo user đơn giản |
| chặn user A đọc project user B | tự so userEmail trong JSON với người gọi mỗi lần | về nguyên tắc đã tách, cần thêm bước double check |
| delete dữ liệu 1 user | lọc + xóa từng file rải rác | Directory.Delete(recursive:true) |
| đổi email của user | không ảnh hưởng đường dẫn project | đổi tên tất cả thư mục liên quan |
| độ phức tạp | thấp, mỗi loại 1 hàm build path cố định | cao hơn, path project phụ thuộc path user, phải build path lồng nhau |
| ảnh | tách theo projectId | nằm sâu trong users/ |
**Chốt** Phẳng
**Trade-offs**
    - hy sinh tốc độ hàm list(liệt kê) đổi lấy các lệnh chạy ngầm và lệnh Lock thường chỉ nhận projectId, dù phải quét toàn bộ projects/ nhưng dự án scope nhỏ --> chấp nhận được, phù hợp right-sizing
    - project.json bắt buộc tự mang userEmail vì path không còn làm việc đó thay
**Xem lại khi** Dự án phức tạp, việc liệt kê tăng đáng kể

## 2.2 userKey= slug+Hash SHA-256 (8 ký tự đầu)
**Bối cảnh** đi kèm với data model
**Options**
| Cách | Được | Mất |
| --- | --- | --- |
| SHA-256 | an toàn tuyệt đối, không cần validate ký tự | tên thư mục vô nghĩa khi debug, phải tra ngược |
| slug+whitelist| đọc được bằng mắt khi debug | tự viết hàm lọc ký tự, dễ sót edge case hoặc email khác nhau nhưng trùng slug |
| GUID riêng làm userId | an toàn, độc lập với email | cần thêm 1 tầng tra cứu email-GUID trước khi tìm path |
**Chốt** Slug + Hash SHA-256 (8 ký tự đầu)
**Trade-offs**
    - Hiểu được nội dung = slug
    - có Hash 8 ký tự --> ~ 4 tỷ giá trị đủ chống collision
    - Đổi việc tên dài hơn thay cho việc thêm 1 tầng tra cứu + collision
**Xem lại khi** Số lượng tăng nhiều

---
## 3. Mô hình pipeline
**Người đề xuất** PLAN.md
**Bối cảnh** quyết định lõi, mọi logic Phase 6-8 dựa vào state này
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. `completedSteps: int` + `runningStep: int?` | Đơn giản; check thứ tự chỉ là `completedSteps == step - 1` | Giả định pipeline tuyến tính; không lưu lịch sử từng lần thử |
| B. Mảng 5 phần tử `[{step, status, startedAt, error}]` | Diễn đạt nhiều hơn; mở đường cho retry history | Nhiều state hơn = nhiều chỗ sai hơn; phải tự giữ bất biến "không có lỗ hổng giữa các bước done" |
**Chốt** completedSteps: int + runningStep: int?
**Trade-offs**
    - việc diễn đạt lịch sử phải đi kèm việc nhiều state hơn --> nhiều nơi có thể sai --> không đánh đánh đổi
    - cách A có completedSteps là 1 con số, không có lỗ hổng ở giữa --> giữ được tính
    - cách B hơn ở việc có thể diễn đạt lịch sử retry nhưng đi kèm có nhiều state hơn=nhiều chỗ để sai hơn --> không đáng đánh đổi ở scope hiện tại
    - yêu cầu right-sizing, đi kèm với tính tuyến tính tuyệt đối của dự án --> A thỏa mãn tuyệt đối
    - in case cần biết đã retry 1 bước bao nhiêu cần --> A có thể mở rộng schema= thêm field retryTimes:0 mà không đụng cấu trúc completedSteps/runningStep đã có --> Điểm mạnh tuyệt đối của A: mở rộng=thêm field, không đổi kiểu dữ liệu
**Xem lại khi** khi cần pipeline tuyến tính (skip step, chạy song song) hoặc lưu lịch sử retry riêng cho từng bước

---
## 4. Nhận diện user
**Người đề xuất** Requirements từ đề
**Bối cảnh** Không yêu cầu auth thật
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. Header `X-User-Email` | Đơn giản nhất, curl dễ | Ai cũng giả mạo được — **không phải bảo mật** |
| B. Cookie phiên ký | Giống thật hơn | Thêm hạ tầng cho thứ đề nói rõ là không cần |
**Chốt** A. Header
**Trade-offs**
    - Cookie có hành vi giống thật nhưng không giải quyết được vấn đề, đề cũng không yêu cầu auth --> Chọn cách đơn giản nhất là A
**Xem lại khi** Không

---
## 5. Chạy việc dài 10-30s
**Người đề xuất** PLAN.md
**Bối cảnh** Việc gọi API Gemini mất nhiều thời gian, không thể bắt HTTP Request đứng đợi gây tốn thread, dễ client timeout + trải nghiệm user tệ
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. `Task.Run` fire-and-forget + tự tạo DI scope | Ít code nhất | Không hàng đợi, shutdown là mất việc, khó quan sát |
| B. `BackgroundService` + `Channel<T>` | Có hàng đợi, shutdown lịch sự, giới hạn được số việc song song | Thêm ~50 dòng và một khái niệm mới |

Cả hai đều thoả FR-23 **nếu** state được persist trước khi chạy — mất việc thì project ở trạng thái "running" và cơ chế stuck (FR-27) sẽ cứu
**Chốt** B. BackgroundService + Channel<T>
**Trade-offs**
    - Đưa vào project này hơi out-of-scope của right-sizing và A phù hợp hơn nhưng đây là dự án học --> chấp nhận
    - B= Nền tảng chuẩn của server-side cần nắm
    - Cả 2 cách đề không thể inject trực tiếp 1 Service được đăng ký Scoped nơi xử lý nền --> Để task chạy nền k mất tài nguyên, k được xài ké Scoped của HTTP Request --> Phải tự xin hệ thống cấp 1 Scope mới + độc lập, xài IServiceScopeFactory, thay vì inject trực tiếp IGeminiClient, sẽ inject IServiceScopeFactory vào hàm/class chạy nền
**Xem lại khi** không

---
## 6. Chống ghi đè
**Người đề xuất** Requirements
**Bối cảnh** dự án dùng file JSON phẳng thay cho database
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. Ghi đè trực tiếp, k lock | code nhanh chỉ 1 dòng | corrupt file gây crash, chắc chắn mất dữ liệu nếu có 2 thao tác cùng lúc, vi phạm FR-24 |
| B. Khóa toàn cục + ghi đè trực tiếp | code dễ, không ai ghi đè | bottleneck: user A lưu project phải chờ user B, corrupt nếu app crash |
| C. khóa theo project + ghi file nguyên tử | tối đa hiệu năng, đảm bảo tính toàn vẹn bằng rename | code phức tạp. Dictionary chứa lock lớn dần gây tốn RAM |
**Chốt** C. Khóa theo project + ghi file nguyên tử = .tmp
**Trade-offs**
    - Chấp nhận ConcurrentDictionary phình to mà không dọn vì scope nhỏ, dung lượng này không đáng kể
    - Cơ chế lock này chỉ chạy trong bộ nhớ 1 instance --> Không chạy được nhiều instance 1 lúc
    - Chấp nhận file rác: Nếu app crash khi đang ghi file tạm, file này sẽ bị bỏ lại trên đĩa chính nhưng file .json vẫn an toàn
**Xem lại khi** Khi dự án cần chạy trên nhiều server (scale-out), khi dung lượng RAM của app tăng vượt mức cho phép

---
## 7. Stack and Storage
**Người đề xuất** Đề bài
**Bối cảnh** Bản v1 dùng SQLite thành công, mục tiêu học thêm disk: toàn vẹn dữ liệu, concurrency, thao tác I/O mà db bình thường cung cấp sẵn
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. .NET + React JS + SQLite | ACID, Concurrency an toàn, truy vấn tốc độ cao | Không học được cách vận hành, kiến trúc cồng kềnh |
| B. .NET + React JS + JSON file on disk | Hiểu sâu vận hành, tính minh bạch tuyệt đối, zero setup | Thiếu ACID, ghi đè tốn kém, query chậm |
**Chốt** B. .NET + React JS + JSON file on disk
**Trade-offs**
    - Không transaction --> tự thiết kế mỗi thao tác chỉ chạm 1 file (docs/03-design.md B1)
    - Không UPDATE...WHERE --> tự làm read-check-write trong lock (docs/03-design.md B6)
    - Không query/index --> quét thư mục, chấp nhận cho dự án scope nhỏ
    - Chỉ đúng khi chạy 1 process --> chấp nhận giả định (docs/03-design.md A6)
**Xem lại khi** Mục tiêu học chuyển thành cần chạy thật, nhiều user cùng lúc --> Khi đó SQLite/Postgres phù hợp hơn

---
## 8. Giữ Project Store
**Người đề xuất** Tôi
**Bối cảnh** Cân nhắc thêm interface cho `ProjectStore` để mock được trong unit test `PipelineService` (Phase 10)
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. Thêm interface | Mock được store trong unit test | Tái tạo đúng abstraction rỗng mà ##1 cố tình tránh |
| B. Giữ ProjectStore cụ thể | Test PipelineService bằng ProjectStore thật --> bằng chứng thuyết phục code hoạt động đúng | Test chạm filesystem thật (trỏ thư mục tạm) --? chạy chậm hơn mock |
**Chốt** B. Giữ ProjectStore cụ thể
**Trade-offs**
    - Lợi ích của A chỉ tiện hơn khi viết Moq setup, giá trị ít hơn việc tái tao6 abstraction rỗng đá cố tránh
    - Test PipelineService dùng Store thật, quay atomic write + lock thật --> chậm hơn, nhưng đổi lại test có giá trị chứng minh cao
**Xem lại khi** Khi cần đổi sang DB hoặc môi trường test cần khác biệt lớn

---
## 9. Schema cũ + bổ sung nhỏ
**Người đề xuất** Tôi
**Bối cảnh** Resume theo item (Portraits/Illustrations) cần biết nhân vật/chương nào đã có ảnh — cân nhắc đổi schema `project.json` hay tận dụng `images[]` sẵn có
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. nested + per-item status (schema mới) | Giải quyết vấn đề resume theo item, tường minh hơn khi đọc JSON | Viết lại toàn bộ B2, B4, A1, A5 đã hoàn thành ở phase 1; tái tạo lại lỗ hổng "giữa trạng thái" ##3 đã loại |
| B. schema cũ + bổ sung | vẫn giải quyết được vấn đề resume theo item, retry logic chỉ cần lọc images.Where(i=>i.step==3) để biết nhân vật nào có ảnh rồi | tính tường minh thấp hơn, phải suy luận trạng thái item từ images[] thay vì đọc thẳng field |
**Chốt** B. Schema cũ + bổ sung
**Trade-offs**
    - Không đổi cấu trúc completedSteps/runningStep --> giữ nguyên tính bất khả thi của trạng thái sai từ ##3
    - Cần thêm 1 đoạn logic mới trong PipelineService khi code Phase 8 (lọc image[] theo step)
    - Giả định ngầm: ảnh cũ của item đã thành công không bị xóa khi retry cả bước, nếu xóa thì resume theo item vô nghĩa
**Xem lại khi** Khi cần lưu thêm thông tin per-item khác mà không suy luận được từ images[]

---
## 10. IGeminiClient trả về kiểu gì cho caller
**Người đề xuất** Claude
**Bối cảnh** Trước khi bắt đầu Phase 5 cần đưa ra quyết định. Khi PipelineService gọi IGeminiClient.GenerateJsonAsync(), cần nhận lại gì để (1) đọc được dữ liệu JSON đã parse, (2) lấy được interaction.id để truyền vào previousInteractionId cho bước sau
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. DTO riêng (GeminiJsonResult, GeminiImageResult) | Che giấu hoàn toàn chi tiết giao thức, PipelineSer chỉ nhận data đã bóc tách sạch, code nghiệp vụ dể đọc | Cần thêm bước định nghĩa class/record DTO ở phase 5, cần viết logic chuyển đổi từ HTTP Response sang DTO trong client thật |
| B. HTTP thô | Không cần DTO, không map | Phá ranh giới kiến trúc: Tầng PipelineSer bị dính chặt vào System.Net.Http, logic parse JSON phân tán khắp pipeline; FakeGeminiClient phải dựng đúng cấu trúc JSON/object mà Gemini SDK trả về | 

**Chốt** A. DTO riêng
**Trade-offs**
    - DoD Phase 5 quy định: bật USE_FAKE_GEMINI=true, ứng dụng phải chạy trơn tru mà không mạng + API Key --> cách A phù hợp
    - Chấp nhận phải viết thêm class/record DTO khi dùng DTO riêng, cái giá thấp hơn việc PipelineSer phụ thuộc vào hình dạng response của Gemini + dựng cấu trúc JSON mà Gemini SDK trả về --> code nhiều hơn nhưng không phá kiến trúc
**Xem lại khi** Không cần

---
## 11. Xác định việc upload file sách (book.txt --> book.uri) có phải là method của IGeminiClient
**Người đề xuất** Claude
**Bối cảnh** Bước 1 pipeline cần gọi Gemini API để đẩy book.txt lên Gemini File API, nhận về book.uri, chỉ gọi 1 lần duy nhất khi project bắt đầu
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. Thêm method thứ 3 vào IGeminiClient | Tập trung một đầu mối: mọi Http tới Gemini API đều quy về 1 interface, code ở Pipeline trực quan | FakeGeminiClient cần giả lập trả uri giả |
| B. Tách thành Service riêng | Tuân thủ ISP, mock/test độc lập | Tăng lượng interface và class trong DI container |
| C. Xử lý nội bộ trong GeminiClient thật | Giấu kín cơ chế khỏi Pipeline | Xử lý nội bộ trong GeminiClient thật --> Không lấy được book.uri để persist, vi phạm FR-23 |
**Chốt** A. Thêm method thứ 3 vào IGeminiClient
**Trade-offs**
    - Chấp nhận viết lại docs cho phù hợp (docs hiện ghi 2 method trong IGeminiClient) để những thứ liên quan nằm cùng 1 chỗ, tiện logic
    - DoD quy định USE_FAKE_GEMINI=true, FakeGeminiClient chỉ cần return chuỗi giả định, không tốn quota --> Cách A phù hợp DoD + cost thấp hơn các cách khác
**Xem lại khi** Không cần

---
## 12. Xác định InteractionID là tham số riêng hay gom vào GeminiRequest object
**Người đề xuất** Claude
**Bối cảnh** Các bước chạy pipeline cần xâu chuỗi ngữ cảnh, khi gọi AI, ngoài prompt và previousInteractionId còn cần Model name, system instruction, JSON schema. Cần thiết kế chữ ký phương thức bền vững
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. Tách riêng | Các hàm trực quan, nhìn interface thấy được tham số | Khi cần chỉnh tham số --> chũ ký vỡ, phải sửa IGeminiClient, GeminiClient, FakeGeminiClient và tất cả mock test |
| B. Gom vào Request object | Tuân thủ Open/Closed Principle: bổ. sung cấu hình mới chỉ cần thêm property vào object mà không đổi chữ ký | Cần tạo thêm class/record Request; thêm 1 lớp trừu tượng cho thứ hiện tại chỉ có 3 field |

**Chốt** B. Gom vào Request object
**Trade-offs**
    - 2 method cũa IGeminiClient có cấu trúc dữ liệu đầu vào khác biệt --> có Request object chuyên biệt giúp tách bạch cấu hình, chữ ký gọn, bất biến với các thay đổi sau này
    - chấp nhận over-engineering đổi lấy việc tuân thủ principle và không phải thay đổi
**Xem lại khi** Không cần

---
## 13. Biểu diễn con trỏ ngữ cảnh trong Project model
**Người đề xuất** Claude
**Bối cảnh** Pipeline cần min 3 con trỏ độc lập(dual-thread docs/02-pipeline.md), hiện Project.cs chỉ có 1 field là ContextRef. Cần:
- book.uri
- last_interaction.id
- last_image_interaction.id
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. 3 field phẳng riêng biệt | Nhất quán triết lý phẳng từ ##2.1, đọc trực tiếp field nào chứa gì | Project.cs có 3 field thay vì 1 |
| B. Đổi ContextRef thành object lồng ContextRefs | Gom 3 giá trị liên quan vào 1 khối, dễ thêm con trỏ thứ 4 (nếu có) | Lệch hẳn field hiện có, thêm 1 class lồng cho đúng 3 giá trị --> thừa với right-sizing |
**Chốt** A. 3 field phẳng riêng biệt
**Trade-offs**
    - Nhất quán triết lý phẳng, không tạo thêm tầng lồng cho chỉ 3 giá trị --> phù hợp yêu cầu right-sizing
**Xem lại khi** Có thêm con trỏ thứ 4 --> option B đáng làm hơn

---
## 14. Thiết kế Request Object cho IGeminiClient
**Người đề xuất** Claude
**Bối cảnh**
- GenerateJsonAsync cần {Model, Prompt, PreviousInteractionId, Schema}
- GenerateImageAsync cần {Model, Prompt, PreviousInteractionId}
--> 3 field chung, 1 field Schema chỉ JSON cần
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. 1 class duy nhất | Độ phức tạp thấp, không trùng lặp code | Nguy cơ bug cao, lỗi chỉ phát hiện ở runtime |
| B. Kế thừa | Độ an toàn cao, không trùng lặp, dễ mở rộng | Phải xủ lý cú pháp kế thừa, tăng độ sâu của dữ liệu | 
| C. 2 class độc lập | Độ an toàn cao, độ phức tạp thấp | Trùng lặp khi Model, Prompt, PreviousInteractionId viết ở cả 2 class; phải sửa 2 nơi khi cần thêm field mới dùng chung |
**Chốt** C. 2 class độc lập
**Trade-offs**
    - Ưu điểm như cách B nhưng độ phức tạp thấp hơn, chấp nhận việc trùng lặp --> phù hợp right-sizing
    - Dự án chỉ 2 loại cần gọi API --> Khả năng không cao xuất hiện thêm class mới
    - SystemInstructions không phải field của GeminiJsonRequest/GeminiImageRequest vì nó không đổi giữa các lần gọi --> đặt ở cấu hình khởi tạo GeminiClient (contructor/DI), không lặp lại mỗi request
    - Lượt gọi đầu tiên (PreviousInteractionId=null, Style) PHẢI đính sách vào input dạn document --> GeminiJsonRequest thêm BookUri (string?) + chỉ điền lượt đầu, sau đó null
    - Bỏ cách GeminiClient tự nhớ book.uri sau UploadBookAsync
    - Chấp nhận việc: BookUri null ở các lượt sau, PipelineService chịu trách nhiệm điền 1st (Style). GeminiImageRequest không có BookUri vì mọi lượt ảnh đều nối từ chuỗi đã có sách

**Xem lại khi** Khi xuất hiện thêm class thứ 3

---
## 15. GenerateImageAsync trả về 1 ảnh hay danh sách ảnh
**Người đề xuất** docs/02-pipeline đưa ra vấn đề
**Bối cảnh** GenerateImageAsync nằm trong IGeminiClient (phase 5 thực hiện), cần quyết định trả nhiều hay 1 ảnh trong GeminiImageResult
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. Chứa danh sách ảnh, để PipelineService tự chọn | Không mất thông tin, linh hoạt nếu sau này cần dùng nhiều ảnh | PipelineService biết luật "lấy ảnh cuối" ==> Logic nghiệp vụ của Gemini bị lộ ngoài tầng client |
| B. Chỉ chứa 1 ảnh, GeminiClient tự lọc lấy ảnh cuối trước khi trả về | IGeminiClient che giao thức, nhất quán với lý do chọn DTO riêng ở ##10; Pipeline không biết về trả ảnh | Sau này cần dùng ảnh khác ngoài "ảnh cuối" --> sửa GeminiClient |
**Chốt** B. Chỉ chứa 1 ảnh
**Trade-offs**
    - Nhất quán với ##10: IGeminiClient giấu giao thức chi tiết, PipelineService chỉ nhận dữ liệu sạch
    - "Lấy ảnh cuối"=luật riêng của Gemini, không phải nghiệp vụ pipeline --> để GeminiClient xử lý
**Xem lại khi** Có yêu cầu cần nhiều hơn 1 ảnh mỗi khi generate

---
## 16. Chọn Interactions API hay generateContent
**Người đề xuất** Claude đối chiếu docs/api-docs.md vs tài liệu Gemini chính thức
**Bối cảnh** ##10-15 dựa trên previousInteractionsId, generateContent là stateless --> không id để nối
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. Interactions API | Giữ nguyên ##10-15 + Fake, test, Project.cs, đúng notebook | API mới (schema đổi); interaction lưu có hạn free 1 day |
| B. generateContent + GeminiClient tự giữ history theo id tự tạo | Giữ interface | RAM mất khi restart (violate FR-23), persist thì thêm 1 tầng lưu, id là fake |
| C. generateContent stateless, prompt tự chứa | Resume miễn phí (style/characters/hình đã có ở project.json), k phụ thuộc retention | Sửa interface + DECISION 10, 12, 14, 15 + Fake + test, gửi lại sách mỗi lượt tốn token, tính nhất quán=cách truyền lại ảnh, không phải "Gemini nhớ được" |
**Chốt** A. Interactions API
**Trade-offs**
    - Giữa nguyên code Phase 5 đã có
    - Resume sau hết hạn, cần dựng lại ngữ cảnh --> Task Phase 8
    - Chưa xác nhận nối text-->ảnh work, bản cũ từng lỗi khi nối --> Check=gọi API thật ở task 5
**Xem lại khi** Bị lỗi khi gọi thật hoặc retention 1 ngày làm resume không dùng được 

---
## 17. document+uri không dùng được khi gọi thật
**Bối cảnh** ##11 và ##14 giả định GeminiJsonRequest.BookUri là cách duy nhất đưa sách vào; Test gọi thật không dùng được document+uri dù đúng format, báo lỗi, nguyên nhân ở Gemini Backend (đã xác nhận ở forum); Đối chiếu proj v1 chỉ nhét=inline text --> cần giải quyết trước Phase 6
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. Giữ document+uri, chờ Google sửa | Đúng FR-29 gốc | Không rõ thời gian fix |
| B. Chuyển sang inline text như v1 | Đã chứng minh hoạt động tốt, không phụ thuộc Files API | Sách thật (~100k token) nhét vào request có thể vượt giới hạn kích thước request |
| C. Thử document+uri, nếu lỗi --> fallback sang inline | Tự phục hồi nếu sau này Google sửa bug | Kéo dài code, thêm 1 nhánh logic để né 1 bug ngoại cảnh only |
**Chốt** B. Chuyển sang inline text như v1
**Trade-offs**
    - Bỏ check bắt buộc BookUri trong GeminiClient.GenerateJsonAsync vì giờ có 2 cách hợp lệ; trách nhiệm đảm bảo lượt đầu có ngữ cảnh chuyển sang caller PipelineService
    - UploadBookAsync tự nó vẫn đúng, chỉ việc dùng nó tham chiếu mới lỗi
    - Chi phí token chỉ tốn 1 lần cho việc gọi Style, tốt hơn cách C khi phải tốn 2 lần nếu bug chưa được fix
**Xem lại khi** Khi Google sửa xong lỗi +test hoạt động thì chuyển về A; Cần đo kích thước sách thật với giới hạn request của Gemini

---
## 18. Style do user nhập nằm ở đâu giữa 202 và worker chạy
**Người đề xuất** Claude (từ FR-31 + docs/03-design.md C5)
**Bối cảnh** User nhập Style, trả về 202 Accepted ngay. Việc thực thi BackgroundService lấy ở Channel<T> diễn ra sau (bất đồng bộ) --> giá trị phải sống qua khoảng đó; FR-23 yêu cầu state nằm trên disk, FR-26 yêu cầu retry đúng bước
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. Trong item của channel | Project.cs và project.json không thêm field | Dữ liệu trong RAM, nếu B1 lỗi + user bấm Retry --> server không nhớ style cũ; app restart mất hết |
| B. Ghi vào project.style ngay lúc claim | Tận dụng field có sẵn | Nhập nhằng trạng thái: project.Style=result hoàn chỉnh của B1, ghi sớm=sai ý nghĩa nếu bước này fail giữa chừng |
| C. Thêm field riêng requestedStyle vào Project.cs | Tách bạch rõ yêu cầu đầu vào (requestedStyle) vs kết quả từ AI (Style); hỗ trợ Retry tốt | Thêm field string? vào Project.cs và file JSON trên đĩa |
**Chốt** C. Thêm field riêng requestedStyle
    - Ghi ở đâu: chỉ bên trong mutate của claim, sau khi check pass, cùng lần ghi với runningStep --> request bị 409 không bao giờ đè được style của request thắng
    - Chỉ ghi khi step==1; claim bước khác không đụng field này
    - Luật retry: mỗi lần claim B1 ghi đúng giá trị request gửi (null=để AI tự chọn). Loại luật "không gửi thì giữ cái cũ" vì null khi đó mang 2 nghĩa, user không quay về "AI tự chọn" được
    - Nhớ style cũ khi retry là việc của FE: GET detail trả requestedStyle, FE điền sẵn vào ô nhập rồi gửi lại tường minh
    - B1 xong: user không nhập --> p.Style=text AI sinh; user có nhập --> p.Style=requestedStyle (bỏ text AI trả, chỉ giữ interaction id để nối ngữ cảnh)
**Trade-offs**
    - Thêm 1 field vào Project.cs + schema B3; đổi lại input của user không mất khi bước lỗi hoặc restart, không nhập nhằng input với kết quả
    - ClaimStepAsync mang 1 tham số chỉ B1 dùng
    - Retry không kèm body (vd curl) sẽ chạy nhánh "AI tự chọn", không dùng lại style cũ --> chấp nhận vì đúng với điều request nói
    - Không tự chạy lại: item trong Channel<T> vẫn mất khi restart (docs/03-design.md A6), user vẫn phải bấm
    - project.json cũ không có field --> đọc ra null, vô hại (A6: không migration)
**Xem lại khi** Khi pipeline mở rộng cho phép user nhập các tham số tùy biến phức tạp cho các bước khác, khi đó cần xem xét gom các tham số user nhập thành object riêng

---
## 19. Lượt chạy cần prove "còn giữ claim" trước khi ghi kết quả hay không
**Người đề xuất** Claude
**Bối cảnh** Claim có giự lock, sau đó gọi Gemini API=ngoài lock, khi Ai trả kết quả Worker quay lại xin lock để ghi. Lock chỉ chống 2 lần ghi cùng lúc, không biết lượt nào còn được giao việc. Áp dụng cho mọi lần ghi của RunStepAsync: ghi kết quả, ghi lỗi, ghi từng ảnh ở phase 7
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. Không kiểm, ghi thẳng | Ít code nhất | Lượt cũ đè lượt mới: chracters + textInteractionId bị thay; lượt cũ lỗi thì ghi failedStep đè lên lượt mới đang chạy tốt |
| B. Check runningStep==step | 1 dòng, k đổi chữ ký; chặn case đã reset mà chưa chạy lại | Không phân biệt 2 lượt của cùng 1 bước |
| C. Vé claim: so runningSince lúc claim với runningSince ở disk | Phân biệt đúng từng lượt; reset -->xóa runningSince=vô hiệu hóa; Phase 8 không phải sửa | Vé phải đi theo claim --> channel item --> RunStepAsync, claim không được trả bool |
**Chốt** C. Vé claim=runningSince
    - Claim ghi runningSince=UtcNow cùng lần ghi với runningStep, trả giá trị đó cho caller
    - Item trong Channel<T> mang projectId, step, vé
    - Mọi lần ghi trong RunStepAsync so vé bên trong mutate, lệch -->false, không ghi gì kể cả lỗi
    - Lượt bị từ chối: bỏ kqua, chỉ log, không ném exception
**Trade-offs**
    - Không cứu được token: lời gọi Gemini AI ở lượt cũ đã gửi, chỉ bảo vệ dữ liệu trên disk
    - Dùng runningSince thay cho Guid riêng: không thêm field, đổi lại 1 field mang 2 role: mốc tgian tính treo + vé; không được làm tròn
    - Trả giá sớm: Phase 6 chưa có force-reset nên so vé chưa fail; làm luôn ở Phase 6 thì chỉ làm 2 bước thay vì để Phase 8 làm 5 bước + ghi từng ảnh
    - Ép quyết định kiểu trả về của claim phải chở được vé
**Xem lại khi** runningSince cần đổi nghĩa (vd cập nhật heartbeat giữa chừng) --> tách vé thành Guid riêng

---
## 20. ClaimStepAsync báo kết quả từ chối bằng gì
**Người đề xuất** Claude
**Bối cảnh**  ClaimStepAsync trả về kiểu dữ liệu gì để:
- Báo được kết quả: Thành công kèm vé claim (runningSince) (##19)
- Báo được: Thất bại kèm lý do để endpoint dựng {error, message} theo C8
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. bool/DataTime? (null=từ chối) | Ít code nhất, khớp UpdateAsync | bool không chở được vé (##19); DateTime chở được nhưng mất lý do |
| B. Exception riêng mang lý do | Đường thành công gọn, trả thẳng vé | Dùng exception để điều khiển luồng nghiệp vụ thông thường như double-click=bad practice; endpoint phải bọc try-catch; test 2 claim song song khó viết |
| C. Result nhỏ (enum state + vé) | Giữ lý do, không exception, map thẳng sang HTTP + error code của C8 | Thêm 1 enum + 1 record; caller có thể quên check trạng thái trước khi dùng vé |
**Chốt** C. Result nhỏ
    - Lý do được gán trong mutate qua biến cpatured; UpdateAsync giữ nguyên chữ ký bool, Storage vẫn không biết khái niệm pipeline
    - Project không tồn tại: UpdateAsync không gọi mutate --> giá trị khởi tạo của trạng thái là "không tồn tại"
    - Endpoint map: thành công -->202, sai thứ tự, đang chạy--> 409, không tồn tại -->404
**Trade-offs**
    - Thêm 1 kiểu mới chỉ để trả về, chấp nhận vì đổi lại message lỗi đúng lý do + không phải đọc state lần 2 ngoài lock
    - Loại bool vì không tương thích ##19, loại exception vì bị từ chối=nghiệp vụ, không phải lỗi hệ thống
**Xem lại khi** Các method khác của PipelineService cũng cần trả kết quả + lý do --> cân nhắc 1 kiểu Result dùng chung

---
## 21. Worker đọc Channel<T> chạy tuần tự hay song song
**Người đề xuất** Claude
**Bối cảnh** ##5 chốt Background Service + Channel<T> nhưng chưa nói bao nhiêu task chạy cùng lúc
**Options**
| Cách | Được | Mất |
|---|---|---|
| A. 1 worker, tuần tự | Ít code nhất, tự giới hạn tốc độ gọi Gemini, test tất định | Project B kẹt sau Project A; UI hiện đang chạy nhưng thật ra đang xếp hàng |
| B. Song song có giới hạn N | Project khác nhau không chờ | Phải chọn N, N lời gọi cùng lúc dễ dính 429, FR-28 cấm auto-retry; test khó tất định |
| C. Mỗi item 1 task, không giới hạn | Không ai chờ ai | Là Task.Run mà ##5 đã loại; không trần; exception bị nuốt, shutdown không chờ |
**Chốt** A. 1 worker, tuần tự, channel unbounded
    - Unbounded: ghi vào luôn thành công --> endpoint không cần đường rollback sau claim
    - Không sợ phình: mội project max 1 item trong hàng (claim chặn item 2nd) --> độ dài hàng <=số project
**Trade-offs**
    - Proj B phải đợi proj A, với cap hiện tại mỗi job 1-2 phút --> chấp nhận cho scope 1 process, ít user
    - runningSince tính cả thời gian xếp hàng; không ghi lại lúc worker bắt đầu vì runningSince là vé (##19) --> ngưỡng treo ở Phase 8 cần cộng tgian chờ
    - Đổi sang B sau này chỉ cần sửa vòng la95p trong BackgroundService, không đụng claim/vé/endpoint
**Xem lại khi** Có nhiều user thật chạy cùng lúc; thời gian chờ thành vấn đề --> chuyển sang B với N nhỏ

---
## 22. placeholder
**Người đề xuất**
**Bối cảnh**
**Options**
| Cách | Được | Mất |
|---|---|---|
**Chốt**
**Trade-offs**
**Xem lại khi** 

---
## Cần có ít nhất các mục sau

- [x] **Stack và storage** — vì sao .NET + React JS; vì sao JSON file thay vì SQLite, dù bản trước đã dùng SQLite thành công. Cái giá: không transaction, không query, chỉ đúng với một process.
- [x] **Mô hình hoá tiến độ pipeline** — `completedSteps` + `runningStep`, hay mảng 5 trạng thái?
- [x] **Chống chạy trùng khi refresh / double-click / hai tab** — lock ở đâu, vì sao client-side là không đủ.
- [x] **Gửi text sách một lần** — chọn cơ chế nào, con trỏ ngữ cảnh persist ra sao, hết hạn thì sao.
- [ ] **Xử lý bước treo** — ngưỡng bao nhiêu, ai phát hiện. **PHASE 8**
- [x] **Right-sizing** — những abstraction đã **cố ý không** thêm (Repository interface, MediatR, tầng Application riêng) và vì sao.

## Chỗ tôi override Claude
**ghi nội dung trước, detail để sau**

- [ ] Override #1: Decision 5 chọn BackgroundService thay vì Task.Run Claude đề xuất --> Chấp nhận viết thêm code để học cái chuẩn
- [ ] Override #2: …
- [ ] Override #3: …

## Chỗ Claude bắt lỗi tôi

- [ ] …
