<![CDATA[<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-purple?style=for-the-badge&logo=dotnet" />
  <img src="https://img.shields.io/badge/Platform-Windows%2010%2B-blue?style=for-the-badge&logo=windows" />
  <img src="https://img.shields.io/badge/UI-WPF-green?style=for-the-badge" />
  <img src="https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge" />
</p>

<h1 align="center">👻 ShadowPilot</h1>

<p align="center">
  <b>Your invisible AI co-pilot for technical interviews on Windows.</b><br/>
  Real-time audio transcription · AI-powered answers · Screenshot analysis · System-wide hotkeys
</p>

---

## ✨ What is ShadowPilot?

ShadowPilot is a **stealth desktop overlay** that sits on top of your screen during technical interviews. It listens to interview questions in real-time, captures screenshots of coding problems, and generates expert-level answers using AI — all triggered by global hotkeys without switching windows.

### Key Features

| Feature | Description |
|---|---|
| 🎙️ **Live Audio Capture** | Captures system audio and transcribes interview questions in real-time using Windows Speech Recognition |
| 🤖 **AI-Powered Answers** | Generates Principal Engineer-level answers using GPT-4o, OpenRouter, or AWS Bedrock (Llama 3.3 70B) |
| 📷 **Screenshot Analysis** | Captures and analyzes on-screen coding problems, system design diagrams, and MCQs |
| ⌨️ **Global Hotkeys** | System-wide keyboard shortcuts that work even when other apps are focused |
| 🔄 **Follow-Up Mode** | Maintains conversation context across multiple questions |
| 🔇 **Whisper Mode** | Reveals answers line-by-line for natural pacing during a call |
| ⏱️ **Auto-Listen** | Automatically detects silence and triggers answer generation |
| 📝 **Write Mode** | Manually type or paste questions directly into the overlay |
| 🫥 **Stealth Overlay** | Non-activating, always-on-top transparent window that never steals focus |
| 🎯 **JD + Resume Context** | Personalizes answers using your job description and resume |

---

## 🚀 Quick Start

### Prerequisites

- **Windows 10** (build 17763) or later
- **.NET 8 SDK** — [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Git** — [Download here](https://git-scm.com/downloads)
- At least one API key: **OpenAI**, **OpenRouter**, or **AWS Bedrock**

---

### Option 1 — One-Click Install (Recommended)

Download the latest `ShadowPilot-Setup.exe` from [**GitHub Releases**](../../releases/latest) and run it. The installer will:
- Set up ShadowPilot in your `AppData` folder
- Create Desktop & Start Menu shortcuts
- Prompt you for an API key on first run

---

### Option 2 — Clone & Build from Source

#### 1. Clone the repository

```powershell
git clone https://github.com/YOUR_USERNAME/ShadowPilot-Windows.git
cd ShadowPilot-Windows
```

#### 2. Set up your API keys

Copy the example env file and add your keys:

```powershell
copy .env.example ShadowPilot\.env
notepad ShadowPilot\.env
```

Fill in at least one API key (see [Environment Variables](#-environment-variables) below).

#### 3. Build and run

```powershell
dotnet restore ShadowPilot/ShadowPilot.csproj
dotnet run --project ShadowPilot/ShadowPilot.csproj
```

#### Or publish a self-contained `.exe`:

```powershell
dotnet publish ShadowPilot/ShadowPilot.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o ./build
```

The compiled `ShadowPilot.exe` will be in the `./build` folder.

---

### Option 3 — PowerShell Installer Script

Run the all-in-one install script (installs .NET 8 if missing, builds, creates shortcuts):

```powershell
# Right-click install.ps1 → "Run with PowerShell"
# Or from terminal:
powershell -ExecutionPolicy Bypass -File install.ps1
```

This will:
1. Check/install .NET 8 SDK
2. Ask for your API key (saved to `%USERPROFILE%\.shadowpilot.env`)
3. Build a self-contained Release binary
4. Install to `%LOCALAPPDATA%\ShadowPilot`
5. Create Desktop & Start Menu shortcuts
6. Register in Add/Remove Programs for easy uninstall

---

## 🔑 Environment Variables

ShadowPilot loads API keys from these locations (in priority order):
1. System/process environment variables
2. `%USERPROFILE%\.shadowpilot.env`
3. `%USERPROFILE%\.env`
4. `.env` file next to the executable

### `.env.example`

```env
# ═══════════════════════════════════════════════════════════════
#  ShadowPilot — Environment Variables
#  Copy this file to ShadowPilot/.env and fill in your keys.
#  At least ONE API key is required.
# ═══════════════════════════════════════════════════════════════

# ── AWS Bedrock (Priority 1 — fastest, uses Llama 3.3 70B) ──
BEDROCK_API_KEY=
BEDROCK_REGION=us-east-1

# ── OpenRouter (Priority 2 — fallback, routes to GPT-4o) ────
OPENROUTER_API_KEY=

# ── OpenAI Direct (Priority 3 — final fallback, GPT-4o) ─────
OPENAI_API_KEY=
```

### API Priority Chain

ShadowPilot tries providers in this order and falls back automatically:

```
Bedrock (Llama 3.3 70B) → OpenRouter (GPT-4o) → OpenAI (GPT-4o)
```

> **💡 Tip:** You only need **one** API key to get started. OpenAI is the easiest — grab a key from [platform.openai.com](https://platform.openai.com/api-keys).

---

## ⌨️ Hotkeys

All hotkeys are **system-wide** — they work regardless of which app is focused.

| Shortcut | Action |
|---|---|
| `Ctrl + Shift + L` | 🎙️ Toggle microphone (start/stop listening) |
| `Ctrl + Shift + A` | 🤖 Get AI answer for current transcript |
| `Ctrl + Shift + D` | 📷 Take screenshot and analyze |
| `Ctrl + Shift + W` | ✏️ Toggle write mode (type questions manually) |
| `Ctrl + Shift + X` | 🗑️ Clear everything |

---

## 🏗️ Project Structure

```
ShadowPilot-Windows/
├── ShadowPilot/                    # Main WPF application
│   ├── App.xaml(.cs)               # Application entry point
│   ├── OverlayWindow.xaml(.cs)     # Stealth overlay UI
│   ├── SetupWindow.xaml(.cs)       # Initial setup screen (JD + Resume)
│   ├── WaveformControl.cs          # Audio waveform visualizer
│   ├── LoadingDotsControl.cs       # Loading animation
│   ├── Controls/                   # Custom UI controls
│   ├── Models/
│   │   └── ConversationTurn.cs     # Chat history model
│   ├── Services/
│   │   ├── AppViewModel.cs         # Core app logic & state management
│   │   ├── GPTService.cs           # OpenAI / OpenRouter streaming API
│   │   ├── BedrockService.cs       # AWS Bedrock Converse API
│   │   ├── EnvConfig.cs            # Multi-source .env loader
│   │   ├── HotkeyManager.cs        # Global hotkey registration
│   │   ├── ScreenshotCapture.cs    # Screen capture service
│   │   ├── SpeechRecognizerService.cs  # Windows Speech Recognition
│   │   ├── SystemAudioCapture.cs   # System audio loopback (NAudio)
│   │   └── SilenceDetector.cs      # Auto-listen silence detection
│   ├── .env                        # Your API keys (git-ignored)
│   ├── app.manifest                # DPI-aware manifest
│   └── ShadowPilot.csproj          # .NET 8 project file
├── .env.example                    # Template for environment variables
├── .gitignore                      # Git ignore rules
├── install.ps1                     # PowerShell one-click installer
├── installer.iss                   # Inno Setup script (CI builds)
├── ShadowPilot.sln                 # Visual Studio solution
└── .github/workflows/
    └── build-installer.yml         # GitHub Actions CI/CD
```

---

## ⚙️ How It Works

1. **Setup Screen** — Paste your Job Description and Resume on first launch
2. **Overlay Mode** — A translucent floating bar appears at the top of your screen
3. **Listen** — Press `Ctrl+Shift+L` to capture system audio during an interview
4. **Answer** — Press `Ctrl+Shift+A` to send the transcript to AI and get an answer
5. **Screenshot** — Press `Ctrl+Shift+D` to capture and analyze coding problems on screen
6. **Read** — Answers appear in the overlay as formatted markdown, right in front of you

The overlay uses Win32 `WS_EX_NOACTIVATE` and `WS_EX_TOOLWINDOW` extended styles so it **never steals focus** and **doesn't appear in the taskbar or Alt+Tab**.

---

## 🛠️ Tech Stack

| Component | Technology |
|---|---|
| Framework | .NET 8 / WPF |
| Audio Capture | NAudio (system loopback) |
| Speech-to-Text | Windows.Media.SpeechRecognition |
| Markdown Rendering | Markdig |
| AI Providers | OpenAI, OpenRouter, AWS Bedrock |
| Installer | Inno Setup + PowerShell |
| CI/CD | GitHub Actions |

---

## 🤝 Contributing

1. **Fork** the repository
2. **Create** a feature branch: `git checkout -b feature/my-feature`
3. **Commit** your changes: `git commit -m "Add my feature"`
4. **Push** to the branch: `git push origin feature/my-feature`
5. **Open** a Pull Request

---

## 📄 License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

---

<p align="center">
  <b>Built with ❤️ for engineers who prepare smarter.</b>
</p>
]]>
