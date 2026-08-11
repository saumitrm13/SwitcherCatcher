Multiplayer Party Game
A fast-paced multiplayer game built in Unity where players take on one of two roles: Switchers who race between poles to protect their territory, and a Catcher who hunts them down.

Gameplay Overview
One player is assigned the Catcher role at the start of each session. The rest are Switchers, each owning one of the coloured poles scattered across the map.
The mini catcher arrives on top of every pole — the pole whose owner is in danger. The pole's owner-switcher must race to a target pole to gather resources and return home before a timer runs out, or their pole explodes and they're eliminated. Meanwhile, the Catcher chases Switchers who are outside their safe zones and tags them out.
Switchers can also form alliances with other Switchers, snatch unoccupied poles, and steal poles from rivals A NavMesh-powered arrow path guides each Switcher toward their current target pole

Key Features

Multiplayer via Unity Netcode for GameObjects — authoritative server model with ServerRpcs and ClientRpcs keeping all game state in sync across clients.
Unity Lobby & Relay — players can create public or private lobbies, browse and join open sessions, and connect peer-to-peer through Unity's Relay service without exposing IPs.
Role System — a single player object supports both Catcher and Switcher roles, switched at runtime via PlayerVisuals and NetworkVariable.
Alliance & Request System — Switchers can send, accept, and break partnership requests with other Switchers through an in-game UI.
Pole Stealing — a contested steal mechanic lets Switchers attempt to take over rivals' poles.
Task Timer — each Switcher assignment has a countdown; failure destroys the pole.
NavMesh Pathfinding — arrow guides rendered along a NavMesh path pointing toward the Switcher's target pole, updated at a configurable interval with object pooling for performance.
Cinemachine — per-player follow cameras using Cinemachine; the camera briefly pans to the problem pole when a new task is assigned.
Score Tracking — a live scoreboard backed by a replicated NetworkList<PlayerScore> visible to all clients.
VFX & Animations — particle effects for valid and invalid pole entries, pole explosions via rigidbody physics, and character fall animations.


Tech Stack
AreaTechnologyEngineUnityNetworkingUnity Netcode for GameObjectsMatchmakingUnity LobbyTransportUnity RelayCameraCinemachinePathfindingUnity NavMeshAuthUnity Authentication (anonymous).

Project Structure
Assets/Scripts/
├── Roles/
│   ├── SwitcherScript.cs        # Core switcher game logic
│   ├── SwitcherRquestHandler.cs # Alliance & request system
│   ├── SwitcherUIScript.cs      # Switcher HUD
│   ├── CatcherScript.cs         # Catcher tagging logic
│   ├── CatcherUIScript.cs       # Catcher HUD
│   └── PathRenderer.cs          # NavMesh arrow path renderer
├── Lobby_And_Relay/
│   ├── LobbyFeatures.cs         # Lobby event subscription & heartbeat
│   ├── LobbyCanvasFunction.cs   # Create / join lobby UI
│   ├── RelayManager.cs          # Relay allocation helpers
│   └── OpenLobbyFunctions.cs    # Public lobby browser
├── Pole.cs / PoleScript.cs      # Pole state & scene component
├── Switcher.cs / Catcher.cs     # Role data classes
├── ScoreManager.cs              # Replicated scoring
├── ScoreBoardUI.cs              # Live scoreboard UI
├── GameStartManager.cs          # Host-side game start & catcher assignment
├── PlayerVisuals.cs             # Runtime role switching visuals
└── DangerVisuals.cs             # Cinemachine-linked problem VFX

