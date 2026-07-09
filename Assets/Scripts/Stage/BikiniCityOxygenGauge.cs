using System;
using System.Collections;
using UnityEngine;

namespace M2.Stage
{
    // 비키니시티 게이지: 산소 (최대 100, 초당 -2 소모, 숨방울로 30% 회복).
    // 고갈 시 30초간 화면 붉어짐 → 게임오버. 그 30초 안에 회복하면 취소.
    public class BikiniCityOxygenGauge : StageGaugeSystem
    {
        [Header("Bikini City Oxygen")]
        [Tooltip("고갈(0) 상태가 이 시간(초) 동안 유지되면 게임오버.")]
        public float gameOverGraceSeconds = 30f;

        // UI hooks into these to show the warning/red-screen and the final game-over state.
        public event Action OnDepletionWarningStarted;
        public event Action OnDepletionWarningCancelled;
        public event Action OnOxygenGameOver;

        Coroutine gameOverCoroutine;

        protected override void HandleDepleted()
        {
            OnDepletionWarningStarted?.Invoke();
            gameOverCoroutine = StartCoroutine(GameOverCountdown());
        }

        protected override void HandleRecovered()
        {
            if (gameOverCoroutine != null)
            {
                StopCoroutine(gameOverCoroutine);
                gameOverCoroutine = null;
                OnDepletionWarningCancelled?.Invoke();
            }
        }

        IEnumerator GameOverCountdown()
        {
            yield return new WaitForSeconds(gameOverGraceSeconds);
            gameOverCoroutine = null;
            OnOxygenGameOver?.Invoke();
        }
    }
}
