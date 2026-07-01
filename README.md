# Tournament DUPR Ratings

A .NET 10 console app that fetches player rosters from a Pickleball Tournaments event and looks up each player's DUPR singles and doubles ratings, outputting the results to the console and an Excel workbook.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A **DUPR Bearer Token** (obtain from your DUPR account)

## Setup 

### Acquire DUPR Bearer Token

1. Navigate to https://dashboard.dupr.com/login
1. Before logging in click "F12" to open Developer view.
1. Click the "Network" tab. 
1. Login to DUPR.
1. After it loads look for any call with the `api.dupr.gg` domain and click it. 
1. This will open the "Headers" view. 
1. Scroll down to the "Request Headers" section
1. Look for "Authorization".
1. We'll need to copy the value AFTER bearer. 
1. It looks like this `Authorization: Bearer <valueToCopy>

## Usage

### Interactive

Run without arguments and the app will prompt for each value:

```bash
dotnet run
```

```
DUPR Bearer Token: ********
Tournament name (slug): colorado-cup-denver-qualifier-by-dpc
```

### With launch arguments

Supply any combination of arguments to skip the corresponding prompts:

```bash
dotnet run -- --bearer-token <token> --tournament-name <tournament-name-from-pbb>
```

| Argument | Description |
|---|---|
| `--bearer-token` | DUPR API bearer token |
| `--tournament-name` | Name of tournament from URL of Pickleball Tournaments |
| `--test-json-result` | (Debug) Path to a previously captured JSON results file |

### VS Code

Fill in the values in `.vscode/launch.json` under the `args` and `env` fields, then press **F5**.

## Output

- **Console** — the raw JSON of processed events and teams
- **Excel** — an `.xlsx` workbook with one sheet per player group/format/age group, written to the
  `REPORT_OUTPUT_PATH` environment variable's directory if set, otherwise to your Documents folder

### Excel columns

Doubles sheets: `Place`, `Player 1 Name`, `Player 1 DUPR ID`, `Player 1 Doubles`, `Player 2 Name`, `Player 2 DUPR ID`, `Player 2 Doubles`, `Average Team DUPR`, `On Waitlist`

Singles sheets: `Place`, `Player Name`, `DUPR ID`, `Singles DUPR`, `On Waitlist`

Rating values show `NR` (not rated) when a player has no rating on record. A `Summary` sheet is
also generated with a color key explaining the cell highlight colors used on each division sheet:

| Color | Meaning |
|---|---|
| White | Player DUPR is within the required range |
| Salmon | Player DUPR does not meet division requirements |
| Yellow (Flavescent) | Player has no partner assigned yet — unable to fully evaluate |
| Purple (DarkOrchid) | Player DUPR rating not found |

## How player lookups work

Each player's DUPR ID is resolved by scraping their profile page on `pickleball.com` (via
their Pickleball Tournaments slug), then that DUPR ID is used to fetch full rating details from
the DUPR API. Lookups happen sequentially and results are cached for the duration of the run so
the same player is never looked up twice.
