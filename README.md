# 🎬 LangV Player

**Language Video Player** — a powerful video player for language learning on Windows.

[![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-blue?style=flat-square)](https://github.com/timursarsembai/LangV-Player/releases)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)](LICENSE)

---

## ✨ Features

- 🎥 **Dual Subtitles** — display two subtitle tracks simultaneously
- 👆 **Interactive Subtitles** — clickable words with instant lookup
- 📚 **Built-in Dictionary** — SQLite database with fast search
- 🤖 **AI Translation** — OpenAI integration for unknown words (planned)
- 📝 **Anki Export** — automatic flashcard creation (planned)
- 🌙 **Dark Theme** — elegant minimalist interface
- 📌 **Always on Top** — pin mode for multitasking

---

## 📥 Installation

### Option 1: Download Release (coming soon)

Pre-built releases will be available on the [Releases](https://github.com/timursarsembai/LangV-Player/releases) page.

### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/timursarsembai/LangV-Player.git
cd LangV-Player

# Build and run
cd LangVPlayer
dotnet restore
dotnet build
dotnet run
```

Or open `langvplayer.sln` in Visual Studio 2022.

---

## ⌨️ Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Space` | Play / Pause |
| `←` / `→` | Seek -10s / +10s |
| `↑` / `↓` | Volume +5 / -5 |
| `Enter` / `F11` | Fullscreen |
| `Esc` | Exit fullscreen |
| `M` | Mute |

---

## 🛠️ Technologies

- **WPF** (.NET 8) — user interface
- **LibVLCSharp** — video playback engine
- **SQLite** — dictionary database
- **Newtonsoft.Json** — settings serialization
- **OpenAI API** — AI translation (planned)

---

## 📋 Roadmap

- ✅ Main window + video playback + dark theme
- ✅ Dual subtitle support
- ⏳ Interactive clickable subtitles
- ⏳ Dictionary popup with translations
- ⏳ Anki flashcard export
- ⏳ AI-powered translations

---

## 📁 Project Structure

```
LangV-Player/
├── LangVPlayer/
│   ├── Helpers/          # Utility classes
│   ├── Models/           # Data models
│   ├── Services/         # Services (settings, subtitles)
│   ├── LangVPlayer.Core/ # Core library
│   ├── App.xaml          # Application resources
│   ├── MainWindow.xaml   # Main window
│   └── LangVPlayer.csproj
├── DEV_LOG.md            # Development journal
└── README.md
```

---

## 📋 System Requirements

| Component | Minimum |
|-----------|---------|
| OS | Windows 10 (x64) |
| RAM | 4 GB |
| .NET | 8.0 Desktop Runtime |

---

## 🤝 Contributing

Found a bug or have an idea? Create an [Issue](https://github.com/timursarsembai/LangV-Player/issues)!

---

## ❤️ Support the Project

If you enjoy this app, consider supporting its development:

[![DonationAlerts](https://img.shields.io/badge/DonationAlerts-Donate-blue?style=for-the-badge)](https://www.donationalerts.com/r/timursarsembai)
[![Liberapay](https://img.shields.io/badge/Liberapay-Donate-yellow?style=for-the-badge)](https://liberapay.com/timursarsembai/donate)

---

## 📄 License

MIT License — free to use!

---

<p align="center">
  <b>Made with ❤️ for language learners</b>
</p>
