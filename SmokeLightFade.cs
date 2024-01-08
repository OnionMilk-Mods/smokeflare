using System.Collections;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.UIElements;

namespace OnionMilk_smokeflare
{
	public class SmokeLightFade : MonoBehaviour
	{
		private Light light;

		private void Awake()
		{
			var particles = GetComponent<ParticleSystem>();
			light = GetComponentInChildren<Light>();

			StartCoroutine(FadeOut(particles.main.duration));
		}

		private IEnumerator FadeOut(float duration)
		{
			float startIntensity = light.intensity;
			float startFade = duration * 0.25f;
			float timer = duration;
			while(timer > 0f)
			{
				if(timer < startFade)
				{
					float t = timer / startFade;
					light.intensity = t * startIntensity;
				}
				yield return null;
				timer -= Time.deltaTime;
			}
			light.intensity = 0f;
		}
	}
}