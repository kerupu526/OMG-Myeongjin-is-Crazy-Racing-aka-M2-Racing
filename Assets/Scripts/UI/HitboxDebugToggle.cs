using UnityEngine;
using UnityEngine.InputSystem;

namespace M2.UI
{
    // 임시 디버그 도구: H 키로 씬의 모든 콜라이더 경계(AABB)를 와이어프레임으로 토글 표시.
    // 충돌 판정이 실제로 어디서 일어나는지 눈으로 확인하기 위한 것 — 정식 게임에는 없는 기능.
    // GL 즉시모드로 매 카메라 렌더마다 직접 그리므로 Scene 뷰의 Gizmos 설정과 무관하게
    // Game 뷰/실제 플레이 화면에서 항상 보임.
    public class HitboxDebugToggle : MonoBehaviour
    {
        public Color wireColor = Color.magenta;

        static Material lineMaterial;
        bool visible;

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        void OnRenderObject()
        {
            if (!visible) return;

            EnsureMaterial();
            lineMaterial.SetPass(0);

            GL.PushMatrix();
            GL.Begin(GL.LINES);
            GL.Color(wireColor);

            foreach (Collider collider in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                DrawWireBounds(collider.bounds);
            }

            GL.End();
            GL.PopMatrix();
        }

        static void DrawWireBounds(Bounds bounds)
        {
            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;
            Vector3[] p =
            {
                c + new Vector3(-e.x, -e.y, -e.z), c + new Vector3(e.x, -e.y, -e.z),
                c + new Vector3(e.x, -e.y, e.z), c + new Vector3(-e.x, -e.y, e.z),
                c + new Vector3(-e.x, e.y, -e.z), c + new Vector3(e.x, e.y, -e.z),
                c + new Vector3(e.x, e.y, e.z), c + new Vector3(-e.x, e.y, e.z),
            };
            int[,] edges =
            {
                { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
                { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
                { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
            };
            for (int i = 0; i < edges.GetLength(0); i++)
            {
                GL.Vertex(p[edges[i, 0]]);
                GL.Vertex(p[edges[i, 1]]);
            }
        }

        static void EnsureMaterial()
        {
            if (lineMaterial != null) return;

            // Built-in shader used for exactly this kind of immediate-mode debug drawing.
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }
    }
}
