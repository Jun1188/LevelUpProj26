using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;


public class Test_tracer : Entity
{
    protected override void Awake()
    {
        base.Initialize();
    }

    public override void TakeDamage(float damageAmount)
    {
        base.TakeDamage(damageAmount);
    }


    public override void FindPath(IInteractable target)
    {
        base.FindPath(target);
    }

    public override void FindByAstar(IInteractable target)
    {
        base.FindByAstar(target);
    }

    protected override void RetracePath(Node startNode, Node endNode)
    {
        base.RetracePath(startNode, endNode);
    }

    protected override IEnumerator FollowPath()
    {
       return base.FollowPath();
    }
    
}
