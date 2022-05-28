using UnityEngine;

namespace RVP
{
    [RequireComponent(typeof(Renderer))]
    [DisallowMultipleComponent]
    [AddComponentMenu("RVP/Damage/Shatter Part", 2)]
    public class ShatterPart : MonoBehaviour
    {
        // -=-=-=-= PUBLISHED STATE =-=-=-=-

        [Tooltip("")]
        public float breakForce = 5;

        [Tooltip("Transform used for maintaining seams when deformed after shattering")]
        public Transform seamKeeper;

        [Tooltip("")]
        public Material brokenMaterial;

        [Tooltip("")]
        public ParticleSystem shatterParticles;

        [Tooltip("")]
        public AudioSource shatterSnd;

        // -=-=-=-= SHARED STATE =-=-=-=-

        [System.NonSerialized]
        public Renderer rend;

        [System.NonSerialized]
        public bool shattered;

        [System.NonSerialized]
        public Material initialMat;

        // Shatter the part
        public void Shatter()
        {
            if (!shattered)
            {
                shattered = true;

                if (shatterParticles)
                {
                    shatterParticles.Play();
                }

                if (brokenMaterial)
                {
                    rend.sharedMaterial = brokenMaterial;
                }
                else
                {
                    rend.enabled = false;
                }

                if (shatterSnd)
                {
                    shatterSnd.Play();
                }
            }
        }

        private void Start()
        {
            rend = GetComponent<Renderer>();
            if (rend)
            {
                initialMat = rend.sharedMaterial;
            }
        }
    }
}