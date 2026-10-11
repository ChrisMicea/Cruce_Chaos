using UnityEngine;

public class OnHoverScript : MonoBehaviour
{
    private bool wasScaledOnHover = false;

    void OnMouseOver()
    {
        //If your mouse hovers over the GameObject with the script attached, output this message
        Debug.Log("Mouse is over GameObject.");

        if (!wasScaledOnHover)
        {
            transform.localScale += new Vector3(2, 2, 2);
            wasScaledOnHover = true;
        }
    }

    void OnMouseExit()
    {
        //The mouse is no longer hovering over the GameObject so output this message each frame
        Debug.Log("Mouse is no longer on GameObject.");

        if (wasScaledOnHover)
        {
            wasScaledOnHover = false;
            transform.localScale -= new Vector3(2, 2, 2);
        }
    }
}
