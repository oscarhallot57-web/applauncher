# App Launcher — lanceur d'applications Windows (Ctrl+Espace)

Lanceur d'applications natif Windows (type Spotlight / PowerToys Run), écrit en **C# / WPF / .NET 8**.

## Pourquoi WPF plutôt que WinUI 3 ou Electron

- **Natif et léger** : pas de runtime Chromium (contrairement à Electron), démarrage quasi instantané.
- **Packaging simple** : contrairement à WinUI 3, WPF ne nécessite pas de packaging MSIX pour tourner en arrière-plan/démarrage automatique — un simple `.exe` suffit.
- **Contrôle fin du rendu** : transparence, coins arrondis, animations et overlay borderless sont matures et simples en WPF.
- **Compatible Windows 10 et 11** sans dépendance supplémentaire.

## Architecture du projet

```
AppLauncher/
├── App.xaml / App.xaml.cs          → démarrage, single-instance, tray, gestion des erreurs globales
├── MainWindow.xaml / .xaml.cs      → overlay (UI + positionnement + animations + clavier)
├── Models/
│   └── ApplicationItem.cs          → une application détectée
├── Services/
│   ├── ApplicationScanner.cs       → scan du Menu Démarrer (.lnk / .url)
│   ├── ShellLinkResolver.cs        → résolution des raccourcis .lnk (COM WScript.Shell, late-binding)
│   ├── ApplicationSearchService.cs → moteur de recherche (exact > préfixe > contient > fuzzy)
│   ├── IconService.cs              → extraction des icônes réelles, en cache, en tâche de fond
│   ├── ApplicationLauncher.cs      → lancement sécurisé (valide le chemin avant d'exécuter)
│   ├── CacheService.cs             → cache JSON (%LocalAppData%\AppLauncher\cache.json)
│   ├── HotkeyService.cs            → raccourci global Windows (RegisterHotKey, Ctrl+Espace)
│   ├── SingleInstanceService.cs    → une seule instance (Mutex + Named Pipe)
│   ├── StartupService.cs           → démarrage avec Windows (clé de registre Run)
│   └── TrayIconService.cs          → icône dans la zone de notification + menu contextuel
├── ViewModels/MainViewModel.cs     → logique MVVM (recherche, sélection, navigation clavier)
├── Helpers/                        → P/Invoke Win32, ObservableObject, RelayCommand, logger de secours
└── Converters/                     → convertisseurs XAML (visibilité)
```

## Comment ça fonctionne

1. Au lancement, une fenêtre invisible est créée uniquement pour obtenir un handle Win32 et
   enregistrer le raccourci global **Ctrl+Espace** (`RegisterHotKey`) — actif même quand une autre
   application a le focus.
2. En parallèle, le cache JSON est chargé instantanément (si présent), puis un scan complet du
   Menu Démarrer (utilisateur + tous les utilisateurs) est relancé en tâche de fond pour rafraîchir
   la liste et re-sauvegarder le cache.
3. Chaque `.lnk` est résolu via l'objet COM `WScript.Shell` (late-binding par réflexion — aucune
   référence COM à configurer dans le `.csproj`), chaque `.url` est lu comme un fichier texte.
4. La recherche est un scoring en mémoire (aucun accès disque), avec 4 niveaux de pertinence :
   correspondance exacte → préfixe → sous-chaîne → fuzzy (sous-séquence).
5. Les icônes réelles sont extraites via `Icon.ExtractAssociatedIcon`, mises en cache mémoire, et
   chargées de façon asynchrone pour ne jamais bloquer l'interface.
6. `Entrée` / double-clic lance l'application sélectionnée (`Process.Start`) puis referme l'overlay ;
   `↑` / `↓` naviguent ; `Échap` ou un second `Ctrl+Espace` referment l'overlay.
7. Une seconde instance ne se lance jamais : elle signale l'instance existante via un pipe nommé
   (pour rouvrir l'overlay), puis se termine immédiatement.

## Compilation et exécution

Prérequis : **Windows 10/11**, **.NET 8 SDK** (https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
cd AppLauncher
dotnet restore
dotnet build
dotnet run
```

## Publier un .exe distribuable (autonome, sans installer .NET sur le poste cible)

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

L'exécutable `publish\AppLauncher.exe` peut ensuite être copié tel quel sur n'importe quel PC Windows 10/11 x64.

Pour une version plus légère qui nécessite le .NET 8 Desktop Runtime déjà installé sur la machine :

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

## Tester

1. `dotnet run` → l'app se lance en arrière-plan (aucune fenêtre visible, une icône apparaît dans
   la zone de notification).
2. Depuis n'importe quelle application (même le navigateur), appuyez sur **Ctrl+Espace** → l'overlay
   apparaît, centré, avec le focus déjà dans la barre de recherche.
3. Tapez par exemple `dis` → les applications correspondantes (ex. Discord) apparaissent en direct.
4. `↓` / `↑` pour naviguer, `Entrée` pour lancer, `Échap` pour fermer sans lancer.
5. Relancez `dotnet run` une seconde fois pendant que la première instance tourne → aucune deuxième
   fenêtre/process ne doit apparaître dans le Gestionnaire des tâches (la première reste active).
6. Clic droit sur l'icône de la zone de notification → « Démarrer avec Windows » pour activer/désactiver
   le lancement automatique.

## Limites connues et pistes d'amélioration (spec §27)

- Le scan couvre le **Menu Démarrer** (utilisateur + tous les utilisateurs), ce qui couvre la
  quasi-totalité des applications visibles dans le menu Démarrer de Windows. L'ajout des entrées de
  registre de désinstallation et des applications UWP (`Get-AppxPackage`) est une extension possible
  si certaines applications spécifiques manquent sur votre machine.
- L'effet de flou Acrylic/Mica natif de Windows 11 n'est pas implémenté (WPF ne l'expose pas
  nativement) ; l'overlay utilise à la place un fond semi-transparent uni, ce qui reste conforme à la
  priorité n°1 du cahier des charges : la stabilité avant l'esthétique.
- La fenêtre a une taille fixe (700×560) plutôt que de s'ajuster dynamiquement au nombre de résultats,
  pour garantir un positionnement à l'écran fiable et sans scintillement au premier passage.
