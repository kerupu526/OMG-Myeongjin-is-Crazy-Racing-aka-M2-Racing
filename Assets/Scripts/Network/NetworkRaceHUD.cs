using M2.Core;
using M2.Items;
using M2.Player;
using M2.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace M2.Network
{
    /// <summary>
    /// Runtime-built online race HUD. The server replicates race state through
    /// <see cref="NetworkRaceManager"/>, while this component presents that state on both the
    /// host and client without relying on the host-only GameManager.
    /// </summary>
    public class NetworkRaceHUD : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.102f, 0.063f, 0.188f, 0.94f);
        static readonly Color Yellow = new Color(1f, 0.851f, 0.239f);
        static readonly Color Pink = new Color(1f, 0.184f, 0.620f);
        static readonly Color Mint = new Color(0.714f, 0.953f, 0.420f);
        static readonly Color Cyan = new Color(0.373f, 0.847f, 0.961f);

        [Header("Legacy generated-scene references")]
        [Tooltip("이전 생성 씬의 중앙 배너. 정식 HUD가 빌드된 뒤에는 숨긴다.")]
        public Text bannerLabel;
        [Tooltip("이전 생성 씬의 정보 라벨. 정식 HUD가 빌드된 뒤에는 숨긴다.")]
        public Text infoLabel;

        NetworkRaceManager raceManager;
        NetworkItemSlots localSlots;
        VehicleController localVehicle;

        GameObject presentationRoot;
        GameObject countdownCard;
        GameObject resultOverlay;
        Text lapLabel;
        Text timerLabel;
        Text versusLabel;
        Text stateLabel;
        Text primaryNameLabel;
        Text secondaryNameLabel;
        Text itemDetailLabel;
        Text supplyLabel;
        Text countdownLabel;
        Text resultTitleLabel;
        Text resultBodyLabel;
        Text resultActionLabel;
        Image primaryIcon;
        Image secondaryIcon;
        Image localAvatar;
        Image opponentAvatar;
        Button rematchButton;
        Button lobbyButton;
        Button mainButton;

        void Awake()
        {
            UiTypography.Apply(bannerLabel, UiFontRole.Display);
            UiTypography.Apply(infoLabel);
        }

        void Start()
        {
            RaceHUD.ConfigureGameplayCanvasScaling(GetComponent<CanvasScaler>());
            BuildPresentation();
            HideLegacyLabels();
            Canvas.ForceUpdateCanvases();
        }

        void Update()
        {
            if (presentationRoot == null) BuildPresentation();

            if (raceManager == null)
            {
                raceManager = FindFirstObjectByType<NetworkRaceManager>();
                if (raceManager == null)
                {
                    ShowWaitingForOpponent();
                    return;
                }
            }

            RefreshPresentation();
        }

        void BuildPresentation()
        {
            if (presentationRoot != null) return;

            presentationRoot = new GameObject("NetworkRaceHudPresentation", typeof(RectTransform));
            presentationRoot.transform.SetParent(transform, false);
            RectTransform rootRect = presentationRoot.GetComponent<RectTransform>();
            Stretch(rootRect, Vector2.zero, Vector2.zero);

            BuildTopCards();
            BuildItemCards();
            BuildCountdownCard();
            BuildResultOverlay();
        }

        void BuildTopCards()
        {
            GameObject lapCard = CreateCard(presentationRoot.transform, "LapCard", Ink, Yellow);
            SetRect(lapCard.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                RaceHUD.ScaleGameplayHud(new Vector2(16f, -16f)), RaceHUD.ScaleGameplayHud(new Vector2(166f, 112f)));
            lapLabel = CreateText("Label", lapCard.transform, 28, Color.white, TextAnchor.MiddleCenter, UiFontRole.Display);
            Stretch(lapLabel.rectTransform, RaceHUD.ScaleGameplayHud(new Vector2(10f, 8f)), RaceHUD.ScaleGameplayHud(new Vector2(-10f, -8f)));

            GameObject timerCard = CreateCard(presentationRoot.transform, "TimerCard", Ink, Pink);
            SetRect(timerCard.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                RaceHUD.ScaleGameplayHud(new Vector2(-16f, -16f)), RaceHUD.ScaleGameplayHud(new Vector2(198f, 112f)));
            timerLabel = CreateText("Label", timerCard.transform, 28, Color.white, TextAnchor.MiddleCenter, UiFontRole.Metric);
            Stretch(timerLabel.rectTransform, RaceHUD.ScaleGameplayHud(new Vector2(10f, 8f)), RaceHUD.ScaleGameplayHud(new Vector2(-10f, -8f)));

            GameObject versusCard = CreateCard(presentationRoot.transform, "VersusCard", Ink, Color.white);
            SetRect(versusCard.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                RaceHUD.ScaleGameplayHud(new Vector2(0f, -16f)), RaceHUD.ScaleGameplayHud(new Vector2(372f, 112f)));

            localAvatar = CreateAvatarChip(versusCard.transform, "LocalAvatar", new Vector2(-145f, 0f));
            opponentAvatar = CreateAvatarChip(versusCard.transform, "OpponentAvatar", new Vector2(145f, 0f));
            versusLabel = CreateText("Players", versusCard.transform, 24, Color.white, TextAnchor.MiddleCenter, UiFontRole.Display);
            SetRect(versusLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                RaceHUD.ScaleGameplayHud(new Vector2(0f, 14f)), RaceHUD.ScaleGameplayHud(new Vector2(260f, 52f)));
            stateLabel = CreateText("State", versusCard.transform, 18, Mint, TextAnchor.MiddleCenter);
            SetRect(stateLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                RaceHUD.ScaleGameplayHud(new Vector2(0f, -31f)), RaceHUD.ScaleGameplayHud(new Vector2(300f, 28f)));
        }

        Image CreateAvatarChip(Transform parent, string name, Vector2 position)
        {
            GameObject chip = new GameObject(name, typeof(RectTransform));
            chip.transform.SetParent(parent, false);
            Image image = chip.AddComponent<Image>();
            image.raycastTarget = false;
            AddOutline(chip, Color.white, new Vector2(2f, -2f));
            SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                RaceHUD.ScaleGameplayHud(position), RaceHUD.ScaleGameplayHud(new Vector2(34f, 34f)));
            return image;
        }

        void BuildItemCards()
        {
            CreateItemCard("PrimaryItemCard", new Vector2(24f, 352f), "1", out primaryIcon, out primaryNameLabel);
            CreateItemCard("SecondaryItemCard", new Vector2(24f, 164f), "2", out secondaryIcon, out secondaryNameLabel);

            GameObject detailCard = CreateCard(presentationRoot.transform, "ItemDetailCard", Ink, Yellow);
            SetRect(detailCard.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                RaceHUD.ScaleGameplayHud(new Vector2(0f, 82f)), RaceHUD.ScaleGameplayHud(new Vector2(590f, 130f)));
            itemDetailLabel = CreateText("Label", detailCard.transform, 22, Color.white, TextAnchor.MiddleLeft);
            Stretch(itemDetailLabel.rectTransform, RaceHUD.ScaleGameplayHud(new Vector2(20f, 12f)), RaceHUD.ScaleGameplayHud(new Vector2(-20f, -12f)));

            supplyLabel = CreateText("SupplyHint", presentationRoot.transform, 20, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter);
            SetRect(supplyLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                RaceHUD.ScaleGameplayHud(new Vector2(0f, 224f)), RaceHUD.ScaleGameplayHud(new Vector2(620f, 30f)));
            AddOutline(supplyLabel.gameObject, Ink, new Vector2(1.5f, -1.5f));
        }

        void CreateItemCard(string name, Vector2 position, string slotNumber, out Image icon, out Text itemName)
        {
            GameObject card = CreateCard(presentationRoot.transform, name, Ink, Yellow);
            SetRect(card.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, RaceHUD.ScaleGameplayHud(position),
                RaceHUD.ScaleGameplayHud(new Vector2(170f, 170f)));

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform));
            iconObject.transform.SetParent(card.transform, false);
            icon = iconObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                RaceHUD.ScaleGameplayHud(new Vector2(0f, 14f)), RaceHUD.ScaleGameplayHud(new Vector2(106f, 106f)));

            itemName = CreateText("Name", card.transform, 20, Color.white, TextAnchor.LowerCenter);
            SetRect(itemName.rectTransform, Vector2.zero, Vector2.one, RaceHUD.ScaleGameplayHud(new Vector2(12f, 6f)),
                RaceHUD.ScaleGameplayHud(new Vector2(-12f, 42f)));

            GameObject badge = CreateCard(card.transform, "SlotNumber", Yellow, Ink);
            SetRect(badge.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
                RaceHUD.ScaleGameplayHud(new Vector2(-8f, 8f)), RaceHUD.ScaleGameplayHud(new Vector2(36f, 36f)));
            Text badgeLabel = CreateText("Label", badge.transform, 22, Ink, TextAnchor.MiddleCenter, UiFontRole.Metric);
            badgeLabel.text = slotNumber;
            Stretch(badgeLabel.rectTransform, Vector2.zero, Vector2.zero);
        }

        void BuildCountdownCard()
        {
            countdownCard = CreateCard(presentationRoot.transform, "CountdownCard", new Color(Ink.r, Ink.g, Ink.b, 0.82f), Yellow);
            SetRect(countdownCard.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                RaceHUD.ScaleGameplayHud(new Vector2(0f, 26f)), RaceHUD.ScaleGameplayHud(new Vector2(520f, 218f)));
            countdownLabel = CreateText("Label", countdownCard.transform, 62, Yellow, TextAnchor.MiddleCenter, UiFontRole.Display);
            Stretch(countdownLabel.rectTransform, RaceHUD.ScaleGameplayHud(new Vector2(24f, 18f)), RaceHUD.ScaleGameplayHud(new Vector2(-24f, -18f)));
            AddOutline(countdownLabel.gameObject, Ink, new Vector2(3f, -3f));
            countdownCard.SetActive(false);
        }

        void BuildResultOverlay()
        {
            resultOverlay = new GameObject("ResultOverlay", typeof(RectTransform));
            resultOverlay.transform.SetParent(presentationRoot.transform, false);
            Image dimmer = resultOverlay.AddComponent<Image>();
            dimmer.color = new Color(0.035f, 0.020f, 0.070f, 0.78f);
            dimmer.raycastTarget = false;
            Stretch(dimmer.rectTransform, Vector2.zero, Vector2.zero);

            GameObject card = CreateCard(resultOverlay.transform, "ResultCard", new Color(Ink.r, Ink.g, Ink.b, 0.97f), Yellow);
            SetRect(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                RaceHUD.ScaleGameplayHud(new Vector2(0f, 12f)), RaceHUD.ScaleGameplayHud(new Vector2(720f, 524f)));
            resultTitleLabel = CreateText("Title", card.transform, 48, Yellow, TextAnchor.MiddleCenter, UiFontRole.Display);
            SetRect(resultTitleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                RaceHUD.ScaleGameplayHud(new Vector2(0f, -58f)), RaceHUD.ScaleGameplayHud(new Vector2(630f, 70f)));
            AddOutline(resultTitleLabel.gameObject, Ink, new Vector2(3f, -3f));
            resultBodyLabel = CreateText("Body", card.transform, 28, Color.white, TextAnchor.MiddleCenter);
            SetRect(resultBodyLabel.rectTransform, Vector2.zero, Vector2.one, RaceHUD.ScaleGameplayHud(new Vector2(36f, 42f)),
                RaceHUD.ScaleGameplayHud(new Vector2(-36f, -210f)));
            rematchButton = CreateResultButton(card.transform, "RematchButton", "다시 하기", new Vector2(-220f, 90f), Pink,
                Color.white, () => raceManager?.RequestRematch());
            lobbyButton = CreateResultButton(card.transform, "ReturnToLobbyButton", "로비로", new Vector2(0f, 90f), Mint,
                Ink, () => raceManager?.RequestReturnToLobby());
            mainButton = CreateResultButton(card.transform, "ReturnToMainButton", "메인으로", new Vector2(220f, 90f), Yellow,
                Ink, ReturnToMain);
            resultActionLabel = CreateText("ActionStatus", card.transform, 17, new Color(1f, 1f, 1f, 0.85f),
                TextAnchor.MiddleCenter);
            SetRect(resultActionLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                RaceHUD.ScaleGameplayHud(new Vector2(0f, 38f)), RaceHUD.ScaleGameplayHud(new Vector2(600f, 28f)));
            resultOverlay.SetActive(false);
        }

        void HideLegacyLabels()
        {
            if (bannerLabel != null) bannerLabel.gameObject.SetActive(false);
            if (infoLabel != null) infoLabel.gameObject.SetActive(false);
        }

        void ShowWaitingForOpponent()
        {
            if (lapLabel == null) return;

            lapLabel.text = "<size=18>LAP</size>\n<size=38>– / –</size>";
            timerLabel.text = "<size=18>TIME</size>\n<size=38>--:--</size>";
            versusLabel.text = $"<color=#{ColorUtility.ToHtmlStringRGB(M2PlayerProfile.AvatarColor)}>{M2PlayerProfile.DisplayName}</color>  <color=#FFD93D>VS</color>  <color=#8BE2FF>상대 대기</color>";
            stateLabel.text = "상대와 연결되면 레이스가 시작됩니다.";
            localAvatar.color = M2PlayerProfile.AvatarColor;
            opponentAvatar.color = Cyan;
            primaryIcon.enabled = false;
            secondaryIcon.enabled = false;
            primaryNameLabel.text = "1 · 연결 대기";
            secondaryNameLabel.text = "2 · 연결 대기";
            itemDetailLabel.text = "<color=#FFD93D>온라인 레이스 준비 중</color>\n방의 두 레이서가 연결되면 실시간 HUD가 표시됩니다.";
            supplyLabel.text = "방 코드 연결 상태를 확인하는 중입니다.";
            countdownCard.SetActive(false);
            resultOverlay.SetActive(false);
        }

        void RefreshPresentation()
        {
            bool localIsHost = LocalIsHost();
            int localLaps = localIsHost ? raceManager.HostLaps : raceManager.ClientLaps;
            int opponentLaps = localIsHost ? raceManager.ClientLaps : raceManager.HostLaps;
            int targetLaps = Mathf.Max(1, raceManager.TargetLapCount);
            NetworkRacerResult localRacer = localIsHost ? raceManager.HostRacer : raceManager.ClientRacer;
            NetworkRacerResult opponentRacer = localIsHost ? raceManager.ClientRacer : raceManager.HostRacer;
            string localName = DisplayNameOr(localRacer, M2PlayerProfile.DisplayName);
            string opponentName = DisplayNameOr(opponentRacer, "상대 레이서");
            Color localColor = ProfileColorOr(localRacer, M2PlayerProfile.AvatarColor);
            Color opponentColor = ProfileColorOr(opponentRacer, Cyan);

            lapLabel.text = $"<size={RaceHUD.ScaleGameplayHudFont(20)}>LAP · 내 바퀴</size>\n" +
                $"<size={RaceHUD.ScaleGameplayHudFont(48)}><color=#FFD93D>{localLaps}</color> / {targetLaps}</size>";
            timerLabel.text = $"<size={RaceHUD.ScaleGameplayHudFont(20)}>TIME · 남은 시간</size>\n" +
                $"<size={RaceHUD.ScaleGameplayHudFont(42)}>{FormatTime(raceManager.TimeRemaining)}</size>";
            versusLabel.text = $"<color=#{ColorUtility.ToHtmlStringRGB(localColor)}>{localName}</color>  <color=#FFD93D>VS</color>  <color=#{ColorUtility.ToHtmlStringRGB(opponentColor)}>{opponentName}</color>";
            stateLabel.text = $"{StateLabel(raceManager.State)} · 상대 {opponentLaps}/{targetLaps}바퀴";
            localAvatar.color = localColor;
            opponentAvatar.color = opponentColor;

            RefreshItemPresentation();
            RefreshStatePresentation(localLaps, opponentLaps, targetLaps, localRacer, opponentRacer,
                localName, opponentName, localColor, opponentColor);
        }

        void RefreshItemPresentation()
        {
            if (raceManager.Mode == RaceMode.Speed)
            {
                primaryIcon.enabled = false;
                secondaryIcon.enabled = false;
                primaryNameLabel.text = "기본 휘발유 · 자동";
                secondaryNameLabel.text = "랜덤 아이템 없음";
                itemDetailLabel.text = $"<color=#FFD93D>기본 휘발유 자동 분사</color>\n레이스 시작 시와 이후 {RaceModeRules.SpeedModeGasolineInterval:0.#}초마다 자동으로 가속합니다. 아이템 슬롯을 차지하지 않습니다.";
                supplyLabel.text = $"스피드전 · 기본 휘발유 {RaceModeRules.SpeedModeGasolineInterval:0.#}초마다 자동 분사";
                return;
            }

            EnsureLocalRefs();
            NetItemId primary = localSlots != null ? localSlots.Primary : default;
            NetItemId secondary = localSlots != null ? localSlots.Secondary : default;
            RefreshSlot(primary, primaryIcon, primaryNameLabel, "1");
            RefreshSlot(secondary, secondaryIcon, secondaryNameLabel, "2");

            ItemDefinition selected = ItemCatalog.CreateFromId(primary) ?? ItemCatalog.CreateFromId(secondary);
            itemDetailLabel.text = selected == null
                ? "<color=#FFD93D>아이템 대기 중</color>\n트랙의 픽업을 획득하면 실제 스프라이트와 상세 설명이 표시됩니다."
                : $"<color=#FFD93D>{selected.itemName}</color>\n{selected.description}";

            string status = "아이템 슬롯 · Ctrl 가속 · E 사용 · P C4 기폭";
            if (localVehicle != null && localVehicle.HasShield) status += " · 방어막";
            if (localVehicle != null && localVehicle.HasSpeedBoost) status += " · 부스트";
            supplyLabel.text = status;
        }

        void RefreshSlot(NetItemId id, Image icon, Text nameLabel, string slotNumber)
        {
            ItemDefinition definition = ItemCatalog.CreateFromId(id);
            if (definition == null)
            {
                icon.enabled = false;
                nameLabel.text = $"{slotNumber} · 비어 있음";
                return;
            }

            bool found = ItemArt.TryGet(definition.id, out Sprite sprite, out Color tint);
            icon.enabled = found;
            icon.sprite = sprite;
            icon.color = tint;
            nameLabel.text = $"{slotNumber} · {definition.itemName}";
        }

        void RefreshStatePresentation(int localLaps, int opponentLaps, int targetLaps,
            NetworkRacerResult localRacer, NetworkRacerResult opponentRacer, string localName,
            string opponentName, Color localColor, Color opponentColor)
        {
            if (raceManager.Result != 0)
            {
                countdownCard.SetActive(false);
                resultOverlay.SetActive(true);
                bool draw = raceManager.Result == 2;
                bool localWon = !draw && IsLocalWinner();
                resultTitleLabel.color = draw ? Pink : localWon ? Yellow : Cyan;
                resultTitleLabel.text = draw ? "무승부" : localWon ? "승리!" : "패배";
                string reason = draw && !string.IsNullOrWhiteSpace(raceManager.DrawReason)
                    ? $"\n{raceManager.DrawReason}" : string.Empty;
                bool showStars = raceManager.CurrentVictoryCondition == VictoryCondition.StarBet;
                resultBodyLabel.text =
                    $"<color=#B6F36B>{BuildRuleLine()}</color>{reason}\n\n" +
                    $"<color=#FFD93D>최종 순위</color>\n" +
                    $"{BuildResultLine(localRacer, localName, localColor, localLaps, targetLaps, showStars)}\n" +
                    $"{BuildResultLine(opponentRacer, opponentName, opponentColor, opponentLaps, targetLaps, showStars)}\n\n" +
                    (draw ? "두 레이서의 기록이 동점으로 처리되었습니다." :
                        localWon ? "가장 먼저 결승 조건을 달성했습니다!" : "상대 레이서가 먼저 결승 조건을 달성했습니다.");
                RefreshResultActions();
                return;
            }

            resultOverlay.SetActive(false);
            RaceState state = raceManager.State;
            if (state == RaceState.Briefing)
            {
                countdownCard.SetActive(true);
                countdownLabel.fontSize = RaceHUD.ScaleGameplayHudFont(30);
                countdownLabel.text = "레이스 준비\n←/→ 조향 · ↑/↓ 가속\nCtrl 아이템 · Shift 드리프트";
            }
            else if (state == RaceState.Countdown || (state == RaceState.Racing && raceManager.Countdown == 0))
            {
                countdownCard.SetActive(true);
                int countdown = raceManager.Countdown;
                countdownLabel.fontSize = RaceHUD.ScaleGameplayHudFont(94);
                countdownLabel.text = countdown > 0 ? countdown.ToString() : "GO!";
            }
            else
            {
                countdownCard.SetActive(false);
            }
        }

        void EnsureLocalRefs()
        {
            if (localSlots != null && localVehicle != null) return;

            NetworkManager manager = NetworkManager.Singleton;
            NetworkObject playerObject = manager != null && manager.LocalClient != null
                ? manager.LocalClient.PlayerObject
                : null;
            if (playerObject == null) return;

            localSlots = playerObject.GetComponent<NetworkItemSlots>();
            localVehicle = playerObject.GetComponent<VehicleController>();
        }

        bool LocalIsHost()
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager != null && manager.LocalClientId == NetworkManager.ServerClientId;
        }

        bool IsLocalWinner()
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager != null && raceManager.WinnerClientId == manager.LocalClientId;
        }

        string BuildRuleLine()
        {
            string mode = raceManager.Mode == RaceMode.Speed
                ? $"스피드전 · {raceManager.TargetLapCount}바퀴 · {raceManager.SpeedModeMaximumKph:0}km/h"
                : $"아이템전 · {raceManager.TargetLapCount}바퀴";
            string victory = raceManager.CurrentVictoryCondition == VictoryCondition.StarBet
                ? "별점 내기" : "단순 완주";
            return $"{mode} · {victory}";
        }

        static string DisplayNameOr(NetworkRacerResult racer, string fallback)
        {
            return racer.HasProfile ? racer.DisplayName : fallback;
        }

        static Color ProfileColorOr(NetworkRacerResult racer, Color fallback)
        {
            return racer.HasProfile ? M2PlayerProfile.ResolveAvatarColor(racer.AvatarColorIndex) : fallback;
        }

        static string BuildResultLine(NetworkRacerResult racer, string displayName, Color color,
            int laps, int targetLaps, bool showStars)
        {
            string placement = racer.Rank > 0 ? $"{racer.Rank}위" : "집계 중";
            string record = racer.Finished
                ? FormatTime(racer.FinishTime)
                : $"미완주 · {laps}/{targetLaps}바퀴";
            string stars = showStars ? $" · ★ {racer.Stars}/6" : string.Empty;
            string plate = racer.HasProfile ? $" {M2PlayerProfile.ResolvePlateLabel(racer.Appearance.PlateIndex)}" : string.Empty;
            return $"<size=24><color=#{ColorUtility.ToHtmlStringRGB(color)}>{placement} {displayName}{plate}</color> · {record}{stars}</size>";
        }

        void RefreshResultActions()
        {
            if (rematchButton == null || lobbyButton == null || mainButton == null) return;
            bool localIsHost = LocalIsHost();
            int localChoice = localIsHost ? raceManager.HostPostRaceChoice : raceManager.ClientPostRaceChoice;
            int opponentChoice = localIsHost ? raceManager.ClientPostRaceChoice : raceManager.HostPostRaceChoice;
            bool awaiting = localChoice != 0;

            rematchButton.interactable = !awaiting;
            lobbyButton.interactable = !awaiting;
            mainButton.interactable = true;
            SetResultButtonLabel(rematchButton, localChoice == 1 ? "다시 하기 · 대기" : "다시 하기");
            SetResultButtonLabel(lobbyButton, localChoice == 2 ? "로비로 · 대기" : "로비로");

            if (resultActionLabel != null)
            {
                resultActionLabel.text = localChoice == 0
                    ? "다시 하기 또는 로비로는 상대 레이서의 같은 선택을 기다립니다."
                    : opponentChoice == 0
                        ? "내 선택을 보냈습니다. 상대 레이서의 응답을 기다리는 중입니다."
                        : opponentChoice == localChoice
                            ? "두 레이서의 선택을 확인했습니다. 화면을 전환하는 중입니다."
                            : "상대 레이서가 다른 선택을 했습니다. 메인으로 나가거나 다시 선택하세요.";
            }
        }

        void ReturnToMain()
        {
            NetworkBootstrapUI bootstrap = FindFirstObjectByType<NetworkBootstrapUI>();
            if (bootstrap != null) bootstrap.ExitSessionToMain();
        }

        static string StateLabel(RaceState state) => state switch
        {
            RaceState.PreRace => "상대 연결 대기",
            RaceState.Briefing => "조작법 안내",
            RaceState.Countdown => "카운트다운",
            RaceState.Racing => "레이싱",
            RaceState.Finished => "결과 확인",
            _ => state.ToString(),
        };

        static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainder = seconds - minutes * 60f;
            return $"{minutes:00}:{remainder:00.00}";
        }

        static GameObject CreateCard(Transform parent, string name, Color fill, Color border)
        {
            GameObject card = new GameObject(name, typeof(RectTransform));
            card.transform.SetParent(parent, false);
            Image image = card.AddComponent<Image>();
            image.color = fill;
            image.raycastTarget = false;
            AddOutline(card, border, new Vector2(3f, -3f));
            return card;
        }

        static Button CreateResultButton(Transform parent, string name, string label, Vector2 position,
            Color fill, Color labelColor, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            image.color = fill;
            AddOutline(buttonObject, Ink, new Vector2(2f, -2f));
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.76f, 0.76f, 0.76f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.42f);
            button.colors = colors;
            if (action != null) button.onClick.AddListener(action);
            SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                RaceHUD.ScaleGameplayHud(position), RaceHUD.ScaleGameplayHud(new Vector2(188f, 56f)));
            Text text = CreateText("Label", buttonObject.transform, 22, labelColor, TextAnchor.MiddleCenter, UiFontRole.Body);
            text.text = label;
            Stretch(text.rectTransform, RaceHUD.ScaleGameplayHud(new Vector2(8f, 4f)),
                RaceHUD.ScaleGameplayHud(new Vector2(-8f, -4f)));
            AddOutline(text.gameObject, labelColor == Ink ? Color.white : Ink, new Vector2(1f, -1f));
            return button;
        }

        static void SetResultButtonLabel(Button button, string label)
        {
            if (button == null) return;
            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
        }

        static Text CreateText(string name, Transform parent, int fontSize, Color color, TextAnchor alignment,
            UiFontRole role = UiFontRole.Body)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            UiTypography.Apply(text, role);
            text.fontSize = RaceHUD.ScaleGameplayHudFont(fontSize);
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = RaceHUD.ScaleGameplayHud(distance);
            outline.useGraphicAlpha = false;
        }
    }
}
