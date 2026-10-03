# Danh sách việc cho bảng tiến độ (Docs/KeHoach_TienDo.xlsx).
# Nhóm (g) -> Main Task (m) -> Sub Task: (Sub Task, Mô tả, Tiến độ %, Ngày hoàn thành).
# Tiến độ là số (0..100), hoặc chữ (ví dụ 'Không làm') cho việc ngoài phạm vi.
# Tạo lại file: powershell -File Tools/Plan/make_plan.ps1 -Out Docs/KeHoach_TienDo.xlsx

$groups = @(
  @{ g = 'Chuẩn bị'; mains = @(
    @{ m = 'Thiết lập project'; rows = @(
      @('Thiết lập project', 'Cấu hình Unity URP, chất lượng hiển thị cho mobile, cấu trúc thư mục và git', 100, '1/10/2026')
    )},
    @{ m = 'Chuẩn bị tài nguyên đồ họa'; rows = @(
      @('Chuẩn bị shader', 'Shader cho đường, cỏ, nền, trái cây, crate, nhấp nháy khi bị cắt', 100, '1/10/2026'),
      @('Chuẩn bị prefab', 'Prefab ô đường, rắn (đầu, đốt), trái cây, crate, hiệu ứng', 100, '1/10/2026'),
      @('Chuẩn bị font và sprite UI', 'Font chữ và các kiểu chữ viền/bóng, sprite nút, khung popup, HUD', 100, '2/10/2026'),
      @('Chuẩn bị shader hiệu ứng', 'Shader cho khói, chớp sáng, lấp lánh và các hạt hiệu ứng khác', 30, ''),
      @('Chuẩn bị tài nguyên cho tính năng sau', 'Prefab, sprite cho cơ chế, booster và các màn hình còn lại', 0, '')
    )},
    @{ m = 'Chuẩn bị âm thanh'; rows = @(
      @('Chuẩn bị âm thanh', '58 âm thanh và nhạc nền, bảng âm lượng/cao độ cho từng âm thanh', 100, '1/10/2026')
    )}
  )},
  @{ g = 'Gameplay – Board & Cốt lõi'; mains = @(
    @{ m = 'Thiết kế board & dữ liệu level'; rows = @(
      @('Đọc dữ liệu bản đồ', 'Đọc bản đồ level dạng Tiled JSON: layer đường, layer vùng sinh trái, vị trí xuất phát; có unit test', 100, '1/10/2026'),
      @('Hệ thống loại ô', 'Bảng 93 loại ô (đường, góc, ngã rẽ, ô dừng, ô giao hàng…) kèm hình hiển thị', 100, '1/10/2026'),
      @('Dữ liệu level', 'Cấu trúc level: cột crate, các đợt trái, thời gian; công cụ import level', 100, '1/10/2026'),
      @('Dựng board và camera', 'Dựng board từ dữ liệu, nền cỏ; camera tự căn board vừa màn hình mọi tỉ lệ', 100, '1/10/2026'),
      @('Hố crate', 'Hố dưới mỗi cột crate, lộ sàn gỗ khi hết crate; ô giao hàng sáng lên khi giao được', 100, '2/10/2026'),
      @('Môi trường theo chủ đề', 'Nền và trang trí khác nhau theo từng khu vực level', 0, '')
    )},
    @{ m = 'Logic rắn & điều khiển'; rows = @(
      @('Di chuyển rắn', 'Chạy 30 ô/giây, tự rẽ ở góc, dừng ở ô đen và ô giao hàng', 100, '1/10/2026'),
      @('Đuôi và ăn trái', 'Trái ăn được nối vào cuối đuôi, các đốt bám theo đường đầu rắn đi', 100, '1/10/2026'),
      @('Điều khiển vuốt', 'Vuốt và giữ tay vuốt tiếp; nhớ lệnh khi đang chạy (hết hạn 1s); chống vuốt nhầm sau khi dừng', 100, '2/10/2026'),
      @('Phát hiện bị kẹt', 'Nhận biết khi rắn không còn nước đi hợp lệ', 0, '')
    )},
    @{ m = 'Logic trái cây & crate'; rows = @(
      @('Giao trái vào crate', 'Mỗi trái 0.048s, crate lấp theo thứ tự, nghỉ khi crate đầy, crate 9 trái', 100, '2/10/2026'),
      @('Sinh trái theo đợt', 'Vị trí cố định theo dữ liệu đã xong; thuật toán tự rải trái theo vùng đang hoàn thiện', 60, ''),
      @('Đợt trái bổ sung', 'Sinh thêm trái khi người chơi thiếu trái để lấp crate', 0, '')
    )},
    @{ m = 'Luật thắng / thua'; rows = @(
      @('Luật cắn đuôi', 'Đầu chạm thân (đuôi từ 5 đốt) là thua NO WAY OUT, khóa thao tác; có test kịch bản', 100, '2/10/2026'),
      @('Cứu bằng Snip', 'Giữ lại 1/5 đuôi tính từ đầu, phần cắt nhấp nháy rồi bay vào crate cùng màu; giá coin cấu hình được', 100, '2/10/2026'),
      @('Level tính giờ', 'Đồng hồ trên HUD, cảnh báo sắp hết giờ, thua khi hết giờ', 0, ''),
      @('Thoát giữa level', 'Xác nhận thoát level đang chơi', 0, '')
    )}
  )},
  @{ g = 'Gameplay – Cơ chế level'; mains = @(
    @{ m = 'Cơ chế điều hướng'; rows = @(
      @('Mũi tên cố định', 'Ô mũi tên ép rắn rẽ theo một hướng', 0, ''),
      @('Mũi tên lật', 'Mũi tên đổi hướng sau mỗi lần đi qua', 0, ''),
      @('Tunnel', 'Đường hầm đưa rắn từ cửa này sang cửa kia', 0, ''),
      @('Ống', 'Ống thẳng và ống cong 90 độ', 0, ''),
      @('Ghi chuyển đường', 'Ghi đổi hướng nhánh đường', 0, '')
    )},
    @{ m = 'Cơ chế cầu & công tắc'; rows = @(
      @('Cầu', 'Cầu vượt: hai đường giao nhau, đi qua cầu không dừng', 0, ''),
      @('Cầu bật/tắt và công tắc', 'Cầu đóng/mở bằng công tắc trên đường', 0, ''),
      @('Ngã tư bật/tắt', 'Ngã tư đổi trạng thái mở/đóng', 0, '')
    )},
    @{ m = 'Cơ chế chướng ngại'; rows = @(
      @('Khối nâng', 'Khối chắn đường hạ xuống sau một số lần giao hàng', 0, ''),
      @('Tường phá được', 'Tường có máu, phá dần; nhóm tường dùng chung máu', 0, ''),
      @('Chặn đường', 'Vật chắn đường và hiệu ứng nổ mở đường', 0, ''),
      @('Mái che', 'Mái che che khuất một phần board', 0, ''),
      @('Rèm che', 'Rèm che trái cây, mở khi đủ điều kiện', 0, '')
    )},
    @{ m = 'Cơ chế thu thập'; rows = @(
      @('Vật phẩm bí mật', 'Vật phẩm ẩn trên board, nhặt được phần thưởng', 0, ''),
      @('Mega Star', 'Ngôi sao đặc biệt trên board và tiến trình thu thập', 0, ''),
      @('Crate phụ', 'Ô crate thêm mở khóa bằng coin', 0, '')
    )},
    @{ m = 'Giới thiệu cơ chế'; rows = @(
      @('Popup giới thiệu cơ chế', 'Giới thiệu khi một cơ chế mới xuất hiện lần đầu', 0, '')
    )}
  )},
  @{ g = 'Gameplay – Booster / Power-ups'; mains = @(
    @{ m = 'Hệ thống booster'; rows = @(
      @('Hệ thống booster chung', 'Hàng booster trên HUD, số lượng, khóa/mở theo level', 0, ''),
      @('Hướng dẫn booster', 'Hướng dẫn cách dùng booster lần đầu, bắt buộc ở level giới thiệu', 0, '')
    )},
    @{ m = 'Tạo booster hỗ trợ'; rows = @(
      @('Boomerang', 'Booster được giới thiệu ở Level 7', 0, ''),
      @('Shuffle', 'Xáo trộn trái trên board', 0, ''),
      @('Xẻng', 'Booster xẻng', 0, ''),
      @('Thu nhỏ', 'Thu ngắn đuôi, tối đa 3 lần mỗi level', 0, ''),
      @('Hoàn tác', 'Quay lại nước vừa đi, tối đa 1 lần mỗi level', 0, ''),
      @('Nam châm', 'Hút trái về phía rắn', 0, ''),
      @('Hẹn giờ', 'Thêm thời gian cho level tính giờ', 0, '')
    )}
  )},
  @{ g = 'Gameplay – Level & Progression'; mains = @(
    @{ m = 'Hệ thống level & progression'; rows = @(
      @('13 level đầu', 'Đưa 13 level đầu vào game; test tự động kiểm tra mọi level đều thắng được', 100, '2/10/2026'),
      @('Chuyển level và lưu tiến độ', 'Danh sách level, lưu tiến độ, chọn level để test, bỏ qua level chưa có tính năng', 100, '2/10/2026'),
      @('Mở rộng lên 353 level', 'Đưa thêm level, kiểm tra từng level chơi được và đúng độ khó', 0, ''),
      @('Độ khó động', 'Phiên bản dễ/khó của level theo kỹ năng người chơi', 0, '')
    )},
    @{ m = 'Hướng dẫn người chơi mới'; rows = @(
      @('Level hướng dẫn đầu game', 'Bàn tay chỉ hướng vuốt và lời nhắn ở level đầu', 0, ''),
      @('Hướng dẫn trong level', 'Bàn tay và lời nhắn theo kịch bản cho các tình huống mới', 0, ''),
      @('Hướng dẫn khi thua', 'Gợi ý tránh tự cắn đuôi ở những level đầu', 0, '')
    )}
  )},
  @{ g = 'Kinh tế & Phần thưởng'; mains = @(
    @{ m = 'Tiền tệ & phần thưởng'; rows = @(
      @('Coin', 'Ví coin lưu lại, hiển thị trên HUD, giá Snip tăng dần mỗi lần dùng', 0, ''),
      @('Phần thưởng thắng level', 'Coin thưởng trên màn VICTORY, hiệu ứng coin bay', 0, ''),
      @('Combo khi giao hàng', 'Chữ WOW / TASTY / SUBLIME và coin thưởng khi giao liên tiếp', 0, ''),
      @('Rương phần thưởng', 'Rương và popup nhận thưởng', 0, ''),
      @('Mua booster bằng coin', 'Dùng coin để mua booster', 0, '')
    )},
    @{ m = 'Mạng sống & chuỗi thắng'; rows = @(
      @('Mạng sống', 'Mất mạng khi thua, hồi mạng theo thời gian', 0, ''),
      @('Chuỗi thắng', 'Đếm chuỗi thắng liên tiếp, popup giới thiệu, hiệu ứng lửa trên HUD', 0, ''),
      @('Mở khóa trái mới', 'Popup khi loại trái cây mới xuất hiện', 0, '')
    )},
    @{ m = 'Sự kiện trong game'; rows = @(
      @('Lava Quest', 'Sự kiện vượt level liên tiếp: tham gia, theo dõi, kết quả, phần thưởng', 0, ''),
      @('Nhiệm vụ tuần', 'Nhiệm vụ hằng tuần và thanh mốc phần thưởng', 0, '')
    )}
  )},
  @{ g = 'UI'; mains = @(
    @{ m = 'Màn chính & UI'; rows = @(
      @('HUD trong game', 'Thanh tiến độ theo số trái đã ăn, chấm mốc mỗi đợt, bảng LEVEL N, căn theo vùng an toàn màn hình', 100, '2/10/2026'),
      @('Màn loading', 'Màn chuyển cảnh khi vào level', 0, ''),
      @('Màn Home', 'Màn chính với nút Play và các lối vào tính năng', 0, ''),
      @('Bản đồ level', 'Hiển thị tiến trình các level đã qua', 0, '')
    )},
    @{ m = 'Popup'; rows = @(
      @('Popup NO WAY OUT / GAME OVER', 'Hiện khi cắn đuôi: nút Snip và Give Up, camera lùi lại; Game Over có Try Again', 100, '2/10/2026'),
      @('Popup VICTORY', 'Chữ VICTORY! bật lên, nền tối dần, nút CONTINUE sang level tiếp', 100, '2/10/2026'),
      @('Popup cứu lần 2', 'Popup "Shorten the tail – Play On" trước khi Game Over', 0, ''),
      @('Popup cài đặt', 'Âm thanh, nhạc, rung', 0, ''),
      @('Popup phụ', 'Xác nhận thoát level, popup thông tin, màn nghỉ giữa các level, tooltip', 0, '')
    )}
  )},
  @{ g = 'Hiệu ứng & Visual'; mains = @(
    @{ m = 'Animation chuyển động'; rows = @(
      @('Hiệu ứng rắn', 'Co giãn khi chạy/dừng, nảy khi dừng, rơi xuống lúc vào level, há/ngậm miệng khi giao hàng', 100, '2/10/2026'),
      @('Hiệu ứng trái', 'Đợt mới nhảy vào lần lượt từ gần rắn ra xa; trái rơi trúng thân rắn chờ bên cạnh rồi nảy về', 100, '2/10/2026')
    )},
    @{ m = 'Feedback gameplay'; rows = @(
      @('Hiệu ứng crate', 'Trái bay vào crate có nảy, crate đầy chớp sáng và pháo giấy, chồng crate trồi lên', 100, '2/10/2026'),
      @('Âm thanh theo sự kiện', 'Gắn âm thanh vào từng sự kiện game: ăn, giao, crate đầy, cắn đuôi, thắng', 100, '1/10/2026'),
      @('Rung và rung camera', 'Rung điện thoại và rung camera theo sự kiện', 0, '')
    )},
    @{ m = 'Particle & hiệu ứng phụ'; rows = @(
      @('Hiệu ứng phụ', 'Trái "thở" khi nằm chờ, hạt lấp lánh trên UI, hiệu ứng chuyển cảnh', 0, '')
    )}
  )},
  @{ g = 'Logic chung & Infrastructure'; mains = @(
    @{ m = 'Hệ thống quản lý game'; rows = @(
      @('Lưu game đầy đủ', 'Lưu coin, booster, mạng, sự kiện, cài đặt', 0, ''),
      @('Đa ngôn ngữ', 'Chữ trong game theo bảng ngôn ngữ', 0, ''),
      @('Tối ưu hiệu năng', 'Giữ FPS ổn định, giảm tạo/hủy object, tải nhanh', 0, '')
    )},
    @{ m = 'Tài liệu dự án'; rows = @(
      @('Tài liệu dự án', 'README, roadmap, ghi chú thiết kế và quyết định kỹ thuật', 100, '2/10/2026')
    )}
  )},
  @{ g = 'Test / QA / Sửa lỗi'; mains = @(
    @{ m = 'Test tự động & thủ công'; rows = @(
      @('Unit test kịch bản', '17 test: đọc bản đồ, ăn/giao trái, cắn đuôi/Snip, nhịp chơi level 2, thắng mọi level', 100, '2/10/2026'),
      @('Chạy trên điện thoại', 'Build Android, test trên máy thật nhiều cỡ màn hình', 0, ''),
      @('Chơi thử theo đợt', 'Chơi thử các level, ghi lại lỗi và cảm giác chưa ổn', 30, '')
    )},
    @{ m = 'Sửa lỗi & hoàn thiện'; rows = @(
      @('Tinh chỉnh cảm giác điều khiển', 'Chỉnh độ nhạy vuốt, tốc độ, nhịp hiệu ứng cho đã tay', 30, ''),
      @('Cân bằng độ khó', 'Đường cong độ khó qua các level, độ dài đuôi, số trái mỗi đợt', 0, ''),
      @('Sửa lỗi theo đợt', 'Gom lỗi sau mỗi đợt chơi thử và sửa', 0, '')
    )}
  )},
  @{ g = 'Nghiên cứu & Sáng tạo'; mains = @(
    @{ m = 'Phân tích gameplay'; rows = @(
      @('Phân tích gameplay', 'Ghi lại điểm hay và điểm chưa hay khi chơi, cảm xúc qua từng đoạn level', 0, '')
    )},
    @{ m = 'Sáng tạo nội dung mới'; rows = @(
      @('Cơ chế mới', 'Thiết kế và làm thử cơ chế level của riêng mình', 0, ''),
      @('Level tự thiết kế', 'Thiết kế level mới dùng cơ chế có sẵn và cơ chế mới', 0, ''),
      @('Chế độ chơi mới', 'Thử chế độ chơi khác (thử thách, giới hạn nước đi…)', 0, ''),
      @('Tăng cảm giác "đã"', 'Thêm hiệu ứng, âm thanh, phản hồi cho những khoảnh khắc đáng nhớ', 0, '')
    )}
  )},
  @{ g = 'Ngoài phạm vi'; mains = @(
    @{ m = 'Không làm'; rows = @(
      @('Quảng cáo', 'Quảng cáo video, hồi mạng bằng quảng cáo, gói bỏ quảng cáo', 'Không làm', ''),
      @('Mua bằng tiền thật', 'Shop, gói ưu đãi, season pass', 'Không làm', ''),
      @('Dịch vụ online', 'Analytics, remote config, A/B test, lưu đám mây', 'Không làm', '')
    )}
  )}
)
