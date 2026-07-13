using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using M2.Core;
using M2.Items;
using M2.Player;
using M2.UI;

namespace M2.Network
{
    // Minimal networked race HUD for Milestone 2a — reads the replicated state off
    // NetworkRaceManager (works identically on host and client) and shows it. Intentionally
    // separate from the local scenes' RaceHUD/RaceFlowUI (which are wired to a live local
    // GameManager); a client's GameManager never runs, so those wouldn't have anything to show.
    // Not final UI — same throwaway-placeholder spirit as the rest of the test HUD.
    public class NetworkRaceHUD : MonoBehaviour
    {
        // Big center banner: countdown number / "GO!" / final result.
        public Text bannerLabel;
        // Top-left info line: state, time left, both lap counts.
        public Text infoLabel;

        NetworkRaceManager raceManager;

        // Milestone 2b: the local player's own inventory/status, resolved lazily once its
        // PlayerObject exists (it spawns a few frames after scene load, like the race manager).
        NetworkItemSlots localSlots;
        VehicleController localVehicle;

        void Awake()
        {
            UiTypography.Apply(bannerLabel, UiFontRole.Display);
            UiTypography.Apply(infoLabel);
        }

        void Update()
        {
            if (raceManager == null)
            {
                // The race manager is spawned by the host at runtime, so it may not exist for
                // the first frames — keep looking until it does.
                raceManager = FindFirstObjectByType<NetworkRaceManager>();
                if (raceManager == null)
                {
                    if (bannerLabel != null) bannerLabel.text = "";
                    if (infoLabel != null) infoLabel.text = "상대를 기다리는 중...";
                    return;
                }
            }

            UpdateBanner();
            UpdateInfo();
        }

        void UpdateBanner()
        {
            if (bannerLabel == null) return;

            if (raceManager.Result == 1)
            {
                bannerLabel.text = IsLocalWinner() ? "승리!" : "패배";
                return;
            }
            if (raceManager.Result == 2)
            {
                string reason = raceManager.DrawReason;
                bannerLabel.text = string.IsNullOrEmpty(reason) ? "무승부" : $"무승부\n{reason}";
                return;
            }

            RaceState state = raceManager.State;
            switch (state)
            {
                case RaceState.Countdown:
                    int c = raceManager.Countdown;
                    bannerLabel.text = c > 0 ? c.ToString() : "GO!";
                    break;
                case RaceState.Briefing:
                    bannerLabel.text = "곧 시작합니다";
                    break;
                case RaceState.Racing:
                    // Countdown value stays 0 (=GO!) for the first moment of Racing, then clears.
                    bannerLabel.text = raceManager.Countdown == 0 ? "GO!" : "";
                    break;
                default:
                    bannerLabel.text = "";
                    break;
            }
        }

        void UpdateInfo()
        {
            if (infoLabel == null) return;

            bool localIsHost = LocalIsHost();
            int myLaps = localIsHost ? raceManager.HostLaps : raceManager.ClientLaps;
            int theirLaps = localIsHost ? raceManager.ClientLaps : raceManager.HostLaps;

            infoLabel.text =
                $"모드: {ModeLabel(raceManager.Mode)} · {raceManager.TargetLapCount}바퀴\n" +
                (raceManager.Mode == RaceMode.Speed
                    ? $"최고 {raceManager.SpeedModeMaximumKph:0}km/h · 기본 휘발유 {RaceModeRules.SpeedModeGasolineInterval:0.#}초마다 자동 분사\n"
                    : "") +
                $"상태: {StateLabel(raceManager.State)}\n" +
                $"남은 시간: {FormatTime(raceManager.TimeRemaining)}\n" +
                $"내 바퀴: {myLaps}\n" +
                $"상대 바퀴: {theirLaps}\n" +
                ItemStatusLines();
        }

        // Local player's two item slots plus any active shield/boost, read straight off the
        // owned vehicle's replicated slots and its status mirrors.
        string ItemStatusLines()
        {
            if (raceManager != null && raceManager.Mode == RaceMode.Speed)
            {
                return $"자동 보급: 기본 휘발유 {RaceModeRules.SpeedModeGasolineInterval:0.#}초마다\n" +
                       "아이템 슬롯: 사용 안 함";
            }

            EnsureLocalRefs();

            string slot1 = "-";
            string slot2 = "-";
            if (localSlots != null)
            {
                slot1 = ItemName(localSlots.Primary);
                slot2 = ItemName(localSlots.Secondary);
            }

            string status = "";
            if (localVehicle != null)
            {
                if (localVehicle.HasShield) status += " 방어막";
                if (localVehicle.HasSpeedBoost) status += " 부스트";
            }

            return $"아이템 1: {slot1}\n아이템 2: {slot2}" +
                   (status.Length > 0 ? $"\n상태:{status}" : "");
        }

        void EnsureLocalRefs()
        {
            if (localSlots != null && localVehicle != null) return;

            var manager = NetworkManager.Singleton;
            NetworkObject playerObject = manager != null && manager.LocalClient != null
                ? manager.LocalClient.PlayerObject
                : null;
            if (playerObject == null) return;

            localSlots = playerObject.GetComponent<NetworkItemSlots>();
            localVehicle = playerObject.GetComponent<VehicleController>();
        }

        static string ItemName(NetItemId id)
        {
            ItemDefinition def = ItemCatalog.CreateFromId(id);
            return def != null ? def.itemName : "-";
        }

        bool LocalIsHost()
        {
            var manager = NetworkManager.Singleton;
            return manager != null && manager.LocalClientId == NetworkManager.ServerClientId;
        }

        bool IsLocalWinner()
        {
            var manager = NetworkManager.Singleton;
            return manager != null && raceManager.WinnerClientId == manager.LocalClientId;
        }

        static string StateLabel(RaceState state) => state switch
        {
            RaceState.PreRace => "대기",
            RaceState.Briefing => "브리핑",
            RaceState.Countdown => "카운트다운",
            RaceState.Racing => "레이싱",
            RaceState.Finished => "종료",
            _ => state.ToString(),
        };

        static string ModeLabel(RaceMode mode) => mode == RaceMode.Speed ? "스피드전" : "아이템전";

        static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainder = seconds - minutes * 60f;
            return $"{minutes:00}:{remainder:00.00}";
        }
    }
}
