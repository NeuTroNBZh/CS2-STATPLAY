# Sources Journal

## 2026-03-26

### VS Code Custom Agents Documentation
- URL: https://code.visualstudio.com/docs/copilot/customization/custom-agents
- Usage: confirmer la structure d'un fichier `.agent.md`, son emplacement workspace dans `.github/agents`, et les champs YAML disponibles.
- Confirmed:
  - les agents personnalisés sont detectes dans `.github/agents`
  - le frontmatter YAML supporte `name`, `description`, `argument-hint`, `model`, `tools`, `agents`, `handoffs`
  - le corps Markdown contient les instructions de fonctionnement de l'agent

### Agent Customization Skill Reference
- URL: copilot-skill:/agent-customization/SKILL.md
- Usage: choisir la bonne primitive de customisation et confirmer les emplacements workspace des fichiers.
- Confirmed:
  - les custom agents workspace vont dans `.github/agents`
  - les instructions workspace vont dans `.github/instructions`
  - les descriptions doivent etre explicites et riches en mots-cles pour la decouverte automatique

### Agent Customization References
- URL: copilot-skill:/agent-customization/references/agents.md
- Usage: definir un agent de recherche strict avec outils minimaux et role clairement borne.
- Confirmed:
  - il faut limiter les outils au strict necessaire
  - un agent efficace doit avoir un role unique et des limites explicites

### File Instructions Reference
- URL: copilot-skill:/agent-customization/references/instructions.md
- Usage: definir un fichier `*.instructions.md` repo-wide pour C#, SQL et configuration.
- Confirmed:
  - les fichiers `*.instructions.md` workspace se placent dans `.github/instructions`
  - `applyTo` peut etre utilise pour cibler les fichiers `.cs`, `.sql` et `.json`
  - une instruction doit couvrir un sujet cohérent et rester concise

### CounterStrikeSharp Primary Documentation
- URL: https://docs.cssharp.dev/docs/guides/getting-started.html
- Usage: source prioritaire definie par la demande utilisateur
- Confirmed:
  - a consulter systematiquement avant toute implementation CS2
  - a completer avec la documentation detaillee et des sources GitHub pertinentes lors des prochaines taches

### CounterStrikeSharp API Utilities
- URL: https://docs.cssharp.dev/api/CounterStrikeSharp.API.Utilities.html
- Usage: confirmer les surfaces officielles pour identifier/lister les joueurs connectes en temps reel.
- Confirmed:
  - `Utilities.GetPlayers()` retourne une liste de `CCSPlayerController` valides avec `UserId` valide.
  - surfaces de mapping joueur disponibles: `GetPlayerFromUserid`, `GetPlayerFromSlot`, `GetPlayerFromSteamId64`.

### CounterStrikeSharp API Server
- URL: https://docs.cssharp.dev/api/CounterStrikeSharp.API.Server.html
- Usage: confirmer les horloges serveur/map et les metadonnees map/capacite pour temps de jeu et historique map.
- Confirmed:
  - `Server.CurrentTime`, `Server.TickedTime`, `Server.EngineTime`, `Server.TickCount`, `Server.TickInterval`.
  - `Server.MapName`, `Server.MaxPlayers`, `Server.GetMapList()`, `Server.IsMapValid(string)`.

### CounterStrikeSharp API Core Events and Models
- URLs:
  - https://docs.cssharp.dev/api/CounterStrikeSharp.API.Core.EventPlayerDeath.html
  - https://docs.cssharp.dev/api/CounterStrikeSharp.API.Core.EventWeaponFire.html
  - https://docs.cssharp.dev/api/CounterStrikeSharp.API.Core.EventRoundStart.html
  - https://docs.cssharp.dev/api/CounterStrikeSharp.API.Core.EventRoundEnd.html
  - https://docs.cssharp.dev/api/CounterStrikeSharp.API.Core.EventPlayerConnectFull.html
  - https://docs.cssharp.dev/api/CounterStrikeSharp.API.Core.EventPlayerDisconnect.html
  - https://docs.cssharp.dev/api/CounterStrikeSharp.API.Core.CCSGameRules.html
  - https://docs.cssharp.dev/api/CounterStrikeSharp.API.Core.CCSPlayerController.html
- Usage: valider les stats V1 directement disponibles ou derivees de maniere fiable.
- Confirmed:
  - kill context detaille via `EventPlayerDeath` (headshot, hitgroup, penetration, noscope, thrusmoke, weapon, etc.).
  - tirs via `EventWeaponFire.Userid`.
  - metadata de round via `EventRoundStart` et `EventRoundEnd`.
  - signaux de cycle joueur via connect/disconnect events.
  - proprietes de timing et de progression round/match exposees dans `CCSGameRules`.

### CounterStrikeSharp Example: With Game Event Handlers
- URL: https://docs.cssharp.dev/examples/WithGameEventHandlers.html
- Usage: confirmer la signature `RegisterEventHandler<T>((@event, info) => HookResult.Continue)` et l'usage de `HookMode`.
- Confirmed:
  - pattern officiel de souscription aux game events via `BasePlugin.RegisterEventHandler`.
  - callback retourne `HookResult`.

### CounterStrikeSharp Example: With Config
- URL: https://docs.cssharp.dev/examples/WithConfig.html
- Usage: confirmer l'implementation plugin config via `IPluginConfig<T>` et `OnConfigParsed`.
- Confirmed:
  - le plugin expose `Config` et implemente `OnConfigParsed(T config)`.
  - possibilite de valider/normaliser la config a ce stade.

### CounterStrikeSharp API BasePlugin
- URL: https://docs.cssharp.dev/api/CounterStrikeSharp.API.Core.BasePlugin.html
- Usage: confirmer `RegisterListener<T>`, `AddTimer(float, Action, TimerFlags?)`, `RegisterEventHandler<T>`.
- Confirmed:
  - timers repetes via `TimerFlags.REPEAT`.
  - listeners globaux via `RegisterListener<Listeners.OnMapStart>`.

### CounterStrikeSharp API EventPlayerDeath
- URL: https://docs.cssharp.dev/api/CounterStrikeSharp.API.Core.EventPlayerDeath.html
- Usage: verifier les noms de proprietes exacts pour mapper les kills detailes.
- Confirmed:
  - proprietes disponibles: `Attacker`, `Userid`, `Assister`, `Weapon`, `Headshot`, `Hitgroup`, `Penetrated`, `Noscope`, `Thrusmoke`, `Distance`, `Attackerblind`, `Attackerinair`, `Assistedflash`.

### MySqlConnector Package
- URL: https://www.nuget.org/packages/MySqlConnector/
- Usage: package ADO.NET MySQL asynchrone pour l'implementation du writer runtime.
- Confirmed:
  - client .NET compatible `net8.0`.
  - support des operations async (`OpenAsync`, `ExecuteNonQueryAsync`, transactions async).

### PowerShell Compress-Archive
- URL: https://learn.microsoft.com/powershell/module/microsoft.powershell.archive/compress-archive
- Usage: generer une archive zip native Windows pour distribuer le plugin avec une arborescence deploiement prete.
- Confirmed:
  - `Compress-Archive` permet de zipper un dossier structure sans dependance externe.
  - convient au packaging local du plugin avant upload ou copie vers serveur.

### GitHub Actions .NET Documentation
- URL: https://docs.github.com/actions/automating-builds-and-tests/building-and-testing-net
- Usage: definir un workflow CI pour restaurer, build et publier les artefacts zip/dossier du plugin.
- Confirmed:
  - `actions/setup-dotnet` et `actions/upload-artifact` couvrent le besoin de build et de publication d'artefacts .NET.
  - un workflow Windows peut lancer directement le script PowerShell de packaging.

### GitHub Release Action
- URL: https://github.com/softprops/action-gh-release
- Usage: attacher automatiquement le zip genere a une GitHub Release lors d'un push de tag `v*`.
- Confirmed:
  - l'action supporte l'upload direct de fichiers d'artefacts produits pendant le workflow.
  - `generate_release_notes` permet une release exploitable sans texte manuel minimal.
