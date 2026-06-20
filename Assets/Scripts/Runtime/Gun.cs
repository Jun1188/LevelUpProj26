using UnityEngine;

public class Gun : MonoBehaviour
{
    public GunData gunData;
    public PlayerController playerController;
    private float lastFireTime = 0f;

    public void Fire()
    {
        if (gunData == null || gunData.bulletPrefab == null || Time.time < lastFireTime + gunData.fireRate)
        {
            Debug.LogWarning("GunData or bulletPrefab is not assigned.");
            return;
        }
        // Instantiate the bullet prefab at the gun's position and rotation
        GameObject bullet = Instantiate(gunData.bulletPrefab, transform.position, transform.rotation);
        // Add force to the bullet to propel it forward
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        rb.AddForce(transform.right * gunData.range, ForceMode.Impulse);

        playerController?.rb.AddForce(-transform.right * gunData.recoilForce, ForceMode.Impulse);

        lastFireTime = Time.time;
    }
}
