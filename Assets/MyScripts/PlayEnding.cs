using UnityEngine;

public class PlayEnding : MonoBehaviour
{
    private AudioSource audioSource;

    void Start()
    {
        // Get the AudioSource attached to this object
        audioSource = GetComponent<AudioSource>();

        // Play the sound
        if (audioSource != null)
        {
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("No AudioSource attached to this GameObject.");
        }
    }
}