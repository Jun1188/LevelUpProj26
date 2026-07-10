using UnityEngine;
using System.Collections;
using System;

public class PlayerInteractionManager : MonoBehaviour
{

    [SerializeField] private Transform playerCamera;

    [SerializeField] TMPro.TextMeshProUGUI promptText;

    [SerializeField] LayerMask interactableLayers;

    private void Update()
    {
        HandleInteractionRaycast();
    }

    private void HandleInteractionRaycast()
    {
        Ray ray = new Ray(playerCamera.position, playerCamera.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 4f, interactableLayers))
        {
            Interactable interactable = hit.collider.GetComponentInParent<Interactable>();

            if (interactable != null)
            {
                if (promptText != null)
                {
                    promptText.gameObject.SetActive(true);
                    promptText.text = $"[E] {interactable.promptMessage}";
                }
                return;
            }
        }

        if (promptText != null) promptText.gameObject.SetActive(false);
    }

}
