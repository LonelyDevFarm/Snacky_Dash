# Cheat System

Module cheat dùng chung, không phụ thuộc game nào. Chép cả thư mục `Assets/CheatSystem` sang game khác là dùng được (cần TextMeshPro + UGUI).
Chỉ hoạt động trong **Editor** và **Development Build**; bản build thường không mở được bảng.

| Phần | Vai trò |
|---|---|
| `Runtime/CheatManager.cs` | Danh sách lệnh theo tab, mở/đóng bảng, tab `System` có sẵn (FPS, tốc độ game, độ phân giải) |
| `Runtime/CheatPanel.cs` + `Resources/CheatPanel.prefab` | Bảng cheat. Sửa màu, cỡ, font thoải mái nhưng **giữ tên và cha** các phần; `Templates` (đang tắt) là hàng mẫu: Tab, Label, Button, Buttons, Input, Toggle |
| `Runtime/CheatButton.cs` | Gắn lên nút bất kỳ (nút cũ vẫn chạy việc của nó). Bấm 5 lần liên tiếp, mỗi lần cách nhau ≤ 0.5 giây, thì mở bảng. Đổi số lần và khoảng cách trên Inspector |
| `Runtime/CheatFps.cs` | Đồng hồ FPS góc trên trái, nhớ bật/tắt |
| `Editor/CheatPanelBuilder.cs` | Menu `Tools/Cheat System/Create Cheat Panel Prefab`: tạo prefab khi chưa có |

## Tích hợp vào một game

1. Tạo một file đăng ký lệnh trong code game (assembly của game tham chiếu asmdef `CheatSystem`):

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
static void Register()
{
    if (!CheatManager.Enabled) return;
    CheatManager.Label("Items", () => $"Coins {Wallet.Coins}");
    CheatManager.Buttons("Items", "Coins", ("+1K", () => Wallet.Add(1000)), ("0", () => Wallet.Set(0)));
    CheatManager.Input("Game", "Go to level", "GO", text => GoTo(int.Parse(text))).Numeric();
    CheatManager.Button("Game", "WIN now", Win).When(() => InLevel);   // chỉ hiện khi đang trong màn
    CheatManager.Button("Other", "Reset ALL data", ResetAll).Confirmed(); // bấm 2 lần mới chạy
    CheatManager.Toggle("Items", "God mode", () => god, on => god = on);
}
```

2. Gắn `CheatButton` lên một nút trên UI.
3. Nếu game đọc chạm/vuốt trực tiếp, tắt nó khi bảng mở: `CheatManager.Opened` / `CheatManager.Closed`.

Ví dụ đầy đủ: `Assets/_MyGame/Runtime/Cheat/SnackDashCheats.cs`.
