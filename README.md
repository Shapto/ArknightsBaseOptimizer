# Arknights Base Optimizer

WPF (.NET 9) tool that plans a 2-4-3 RIIC base for my roster, optimized for once-a-day check-ins.

- Balanced / LMD / EXP buttons, two alternating shifts, 20 dorm beds
- Accounts for Factory storage and Trading Post order limits, morale, gold supply, and synergies
- "Everyone max promotion" preview shows which promotions would actually help

Data: `Data/roster.json` (my operators) and `Data/base_skills.json` (base skills from the game data).
Operator icons are downloaded on first run and cached in `%LocalAppData%\ArknightsBaseOptimizer\Avatars`.
