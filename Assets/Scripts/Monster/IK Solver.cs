using UnityEngine;

public class IKSolver : MonoBehaviour
{
    [SerializeField] Transform shoulder, elbow;
    [SerializeField] float r1, r2;
    [SerializeField] Transform beginTran, endTran;
    [SerializeField] Transform upReference;

    void Update()
    {
        doIK();
    }

    public void doIK()
    {
        //Initial variables
        float d = Vector3.Distance(beginTran.position, endTran.position);

        //Move shoulder to position
        shoulder.position = beginTran.position;

        //Check for endpoint out of range
        if(d >= r1 + r2)
        {
            shoulder.LookAt(endTran.position);
            elbow.transform.position = beginTran.position + (shoulder.forward * r1);
            elbow.LookAt(endTran.position);
            return;
        }

        //Find 2D rotation values
        float fracNum = (r2 * r2) - (d * d) - (r1 * r1);
        float fracDen = -2f * d * r1;
        float theta = Mathf.Acos(fracNum / fracDen);

        //Perform 2D rotation
        shoulder.LookAt(endTran.position);
        //Vector3.up is the pole vector here
        Vector3 axis1 = Vector3.Cross((endTran.position - beginTran.position).normalized, upReference.up).normalized;
        //MUST rotate in world space, otherwise LookAt messes everything up since the calculations are done before that (and other things?)

        //shoulder.Rotate(axis1, Mathf.Abs(Mathf.Rad2Deg * theta), Space.World);
        Quaternion q = Quaternion.AngleAxis(Mathf.Abs(Mathf.Rad2Deg * theta), axis1);
        shoulder.rotation = q * shoulder.rotation;

        elbow.transform.position = beginTran.position + (shoulder.forward * r1);
        elbow.LookAt(endTran.position);
    }
}
