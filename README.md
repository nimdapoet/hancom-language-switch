# Hancom Language Switch 1.0

Công cụ độc lập để chọn giao diện English hoặc Korean cho Hangul/HWP và Hanword thuộc Hancom Office 2022 (12.x). Công cụ dùng các tệp tài nguyên ngôn ngữ đã có sẵn trong bản cài của bạn; không phải gói dịch mới hay sản phẩm chính thức của Hancom.

## Sử dụng

1. Lưu tài liệu và đóng các ứng dụng Hancom, bao gồm tiện ích `한컴 기본 설정`. Nếu máy có cả Hancom 2010, hãy đóng bản đó trước khi chuyển.
2. Chạy `HancomLanguageSwitch.exe` bằng cách nhấp đúp. Đây là bản chạy trực tiếp, không cần cài đặt hoặc quyền quản trị.
3. Giữ chọn **Hangul / HWP (한글)**. Chọn thêm **Hanword (한워드)** nếu bạn muốn đổi ngôn ngữ cho cả ứng dụng đó.
4. Bấm **Use English**, sau đó **Open Hangul** hoặc **Open Hanword**. Các nút mở ứng dụng dùng đường dẫn của bản 2022.
5. Khi muốn trở lại tiếng Hàn, đóng các ứng dụng Hancom rồi bấm **Use Korean**. Nút **Restore original** khôi phục lựa chọn ngôn ngữ trước lần thay đổi đầu tiên của công cụ, đồng thời giữ những thiết lập khác đang có.

## Phạm vi thay đổi và sao lưu

- Chỉ sửa lựa chọn `CultureName` của các ứng dụng được chọn trong `%APPDATA%\HNC\User\Common\120\ThemeInfo.xml`.
- Giữ nguyên ngôn ngữ mặc định chung, theme và thiết lập của ứng dụng không được chọn.
- Không sửa DLL, tệp thực thi, bản quyền hay tài liệu `.hwp`/`.hwpx`/`.docx`.
- Trước lần thay đổi đầu tiên, lưu bản gốc và mã kiểm tra SHA-256 tại `%LOCALAPPDATA%\HancomLanguageSwitch\2022\`.
- Trước mỗi lần ghi, lưu thêm một bản sao cấu hình. Ghi thay thế nguyên tử và từ chối nếu cấu hình bị ứng dụng khác sửa trong lúc thực hiện.
- Không tự đóng ứng dụng hay gửi nội dung tài liệu lên mạng.
- Muốn bỏ công cụ, dùng **Restore original** nếu cần rồi xóa thư mục chứa công cụ. Bản sao lưu vẫn được giữ tại đường dẫn trên.

## Kết quả kiểm tra trên máy này — 28/10/2026

- Hangul `Hwp.exe` phiên bản **12.0.0.535** và `Hwp.String.en-US.dll` cùng phiên bản.
- Sau khi đặt riêng `/root/Hwp/ThemeInfo/CultureName/@value` thành `en-US`, ứng dụng Hangul 2022 đã hiển thị các menu **File, Edit, View, Insert, Format, Pages, Security, Review, Tools** và các nút **Font, Paragraph, Page Margins, Columns, Picture, Table** bằng tiếng Anh.
- Kiểm tra trên tài liệu trắng `verification/LanguageCheck.hwpx`, được sao chép từ mẫu Normal đi kèm Hancom. Tài liệu này không được đóng gói cùng công cụ.
- Không xác nhận rằng tất cả hộp thoại đều có bản dịch đầy đủ. Một số tên phông chữ, nội dung mẫu hoặc thành phần có thể vẫn là tiếng Hàn.
- Chức năng cho Hanword có kiểm tra tệp tài nguyên và kiểm thử cấu hình; chưa kiểm tra giao diện Hanword trực tiếp trên máy này.
- 28 kiểm tra tự động đã vượt qua, gồm giới hạn ứng dụng/ngôn ngữ, bảo toàn cấu hình, hoàn tác, sao lưu hỏng, tệp XML hỏng, ứng dụng đang chạy và xung đột ghi.

## Mã nguồn và biên dịch

Mã C# dùng .NET Framework có sẵn trên máy Windows đang chạy Hancom 2022. Chạy `build.ps1` trong PowerShell để tạo các tệp trong `dist`. Không cần tải thêm thư viện bên ngoài.

`HancomLanguageSwitch.Cli.exe` là bản dòng lệnh dùng cùng mã xử lý, hỗ trợ `--status`, `--english`, `--korean`, `--restore`; đối số thứ hai tùy chọn là `Hwp` hoặc `Hwp,Hword`. Bản GUI là lựa chọn thông thường.

`tools/InspectIL.cs` là tiện ích đọc IL ở chế độ reflection-only được dùng trong quá trình khảo sát. Không cần tiện ích này để chạy hoặc biên dịch bộ chuyển đổi. Bản đóng gói không chứa DLL hoặc mã nhị phân của Hancom.
