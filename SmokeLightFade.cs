using System.Collections;
using UnityEngine;

namespace OnionMilk_smokeflare
{
	public class SmokeLightFade : MonoBehaviour
	{
		private Light light;

		private void Start()
		{
			var particles = GetComponent<ParticleSystem>();
			light = GetComponentInChildren<Light>();
			
			StartCoroutine(FadeOut(particles.main.duration));
		}

		public void PlayNoise()
		{
			RoundManager.Instance.PlayAudibleNoise(
				transform.position,
				Plugin.cfgNoiseRange.Value,
				Plugin.cfgNoiseLoudness.Value,
				1,
				StartOfRound.Instance.hangarDoorsClosed
			);
		}

		private IEnumerator FadeOut(float duration)
		{
			PlayNoise();
			
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