# Copilot Environment Variables Switcher

A small cross-platform desktop app (Avalonia, .NET 10) for switching the environment variables that configure GitHub Copilot CLI to use a custom model provider (BYOK), without editing system settings or shell profiles by hand.

## Variables managed

| Variable | Required | Purpose |
|---|---|---|
| `COPILOT_PROVIDER_BASE_URL` | Yes | Base URL of the provider API |
| `COPILOT_MODEL` | Yes | Model name to use |
| `COPILOT_PROVIDER_TYPE` | Optional | Provider type (`openai`, `azure`, `anthropic`) |
| `COPILOT_PROVIDER_API_KEY` | Optional | API key (masked by default) |

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows or macOS (Linux writes shell profiles only)

## Run

```
dotnet run
```

## Usage

1. Pick a **Base URL** and **Model** from the dropdowns.
2. Optionally tick **Set this variable** for the provider type and/or API key, then choose/enter a value. Use **Show API key** to reveal the key.
3. Click **Apply** to save the values. Unticked optional variables are removed.
4. Click **Clear All** to remove all four variables.
5. **Restart your terminal** (or any app that needs the variables) so it picks up the changes.

A blank option in a dropdown clears that variable.

## Where values are stored

- **Windows:** user-level environment variables (current user).
- **macOS/Linux:** a managed block in `~/.zshrc` and `~/.bash_profile`, delimited by `# >>> copilot-env-switcher >>>` / `# <<< copilot-env-switcher <<<`. On macOS the values are also set via `launchctl` for GUI apps.

## Customizing dropdown choices

Edit the text files in the `Lookups` folder (copied next to the executable on build):

- `baseurls.txt`, `models.txt`, `providers.txt`
- One entry per line, either `Value` or `Display Text,Value`.
- A blank first line adds an empty option that clears the variable.

Example `baseurls.txt` line: `Ollama,http://localhost:11434/v1`

## Security note

The API key is stored in plain text in the user environment / shell profile, like any environment variable. Don't share those files.
