using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class TNode : MonoBehaviour
{
    public TNode parent;
    Quaternion localR = Quaternion.identity;

    [SerializeField] Vector3 localT, localS =  Vector3.one;
    [SerializeField] BoxCollider col;

    void Update()
    {
        if(col) col.center = GetWorldPosition();

        if(GetComponent<Renderer>())
        {
            GetComponent<Renderer>().bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
            GetComponent<Renderer>().material.SetMatrix("MyTRSMatrix", GetWorldMatrix());
        }
    }

    public Matrix4x4 GetWorldMatrix()
    {
        if(parent != null)
        {
            return parent.GetWorldMatrix() * GetLocalMatrix();
        }
        else
        {
            return GetLocalMatrix();
        }
    }

    Matrix4x4 GetLocalMatrix()
    {
        return Matrix4x4.Translate(localT) * Matrix4x4.Rotate(localR) * Matrix4x4.Scale(localS);
    }

    public Vector3 GetRight()
    {
        return ((Vector3)(GetWorldMatrix().GetColumn(0))).normalized;
    }

    public Vector3 GetUp()
    {
        return ((Vector3)(GetWorldMatrix().GetColumn(1))).normalized;
    }

    public Vector3 GetForward()
    {
        return ((Vector3)(GetWorldMatrix().GetColumn(2))).normalized;
    }

    public Vector3 GetLocalPosition()
    {
        return localT;
    }

    public Vector3 GetWorldPosition()
    {
        if (parent != null)
            return parent.GetWorldMatrix().MultiplyPoint3x4(localT);
        return localT;
    }


    public void SetWorldPosition(Vector3 worldPos)
    {
        if (parent != null)
        {
            localT = parent.GetWorldMatrix().inverse.MultiplyPoint3x4(worldPos);
        }
        else
        {
            localT = worldPos;
        }
    }

    public void Translate(Vector3 translation)
    {
        localT.x += translation.x;
        localT.y += translation.y;
        localT.z += translation.z;
    }

    public void SetRotation(Quaternion q)
    {
        if (float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w))
        {
            Debug.Log("NaN");
            return;
        }

        localR = q;
    }


    public void RotateLocal(Quaternion q)
    {
        localR = localR * q;
    }

    public void RotateWorld(Quaternion q)
    {
        localR = q * localR;
    }

    public Quaternion GetRotation()
    {
        return parent != null ? parent.GetRotation() * localR : localR;
    }

    public void LookAt(Vector3 position)
{
    LookAt(position, Vector3.up);
}

    public void LookAt(Vector3 position, Vector3 up)
{
    Vector3 forward = position - GetWorldPosition();
    if (forward.sqrMagnitude < 0.0000001f) return;

    Quaternion worldRotation = Quaternion.LookRotation(forward, up);

    if (parent != null)
    {
        // Convert world rotation to local rotation
        Quaternion localRotation = Quaternion.Inverse(parent.GetRotation()) * worldRotation;
        SetRotation(localRotation);
    }
    else
    {
        SetRotation(worldRotation);
    }
}


    public Vector3 InverseTransformPoint(Vector3 point)
    {
        Matrix4x4 inverse = GetWorldMatrix().inverse;
        return inverse.MultiplyPoint3x4(point);
    }

    public Vector3 TransformPoint(Vector3 point)
    {
        Matrix4x4 mat = GetWorldMatrix();
        return mat.MultiplyPoint3x4(point);
    }

    public Vector3 TransformDirection(Vector3 direction)
    {
        return GetWorldMatrix().MultiplyVector(direction);
    }

    public Vector3 InverseTransformDirection(Vector3 direction)
    {
        return GetWorldMatrix().inverse.MultiplyVector(direction);
    }

    public Vector3 TransformVector(Vector3 vector)
{
    return GetWorldMatrix().MultiplyVector(vector);
}
}
