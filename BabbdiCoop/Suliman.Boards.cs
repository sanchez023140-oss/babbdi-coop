using UnityEngine;

namespace BabbdiCoop
{
    /// <summary>Boarded-up window: four planks and two battens. Breaks when the player
    /// stands next to it and swings/uses (LMB or E), leaving a few physical splinters.</summary>
    internal sealed class SulimanBoards : MonoBehaviour
    {
        private bool broken;

        private void Update()
        {
            if (broken) return;
            if (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.E)) return;
            var fpc = FirstPersonController.instance;
            if (fpc == null) return;
            if ((fpc.transform.position - transform.position).sqrMagnitude > 9f) return;
            Break();
        }

        private void Break()
        {
            broken = true;
            for (int i = 0; i < 5; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "board_splinter";
                go.transform.position = transform.position +
                    new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.5f, 0.5f), Random.Range(-0.25f, 0.25f));
                go.transform.rotation = Random.rotation;
                go.transform.localScale = new Vector3(Random.Range(0.35f, 0.7f), 0.10f, 0.08f);
                var r = go.GetComponent<Renderer>();
                if (r != null && Suliman.WoodDarkMat != null) r.sharedMaterial = Suliman.WoodDarkMat;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.35f;
                rb.AddForce(new Vector3(Random.Range(-2f, 2f), Random.Range(1.2f, 3f), Random.Range(-2f, 2f)),
                            ForceMode.Impulse);
                Destroy(go, 5f);
            }
            Destroy(gameObject);
        }
    }
}
