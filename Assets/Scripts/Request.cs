using UnityEngine;

public class Request
{
    public PoleType SentByPoleType;
    public PoleType SentToPoleType;

    public bool isAccepted;

    public Request(PoleType sentByPoleType, PoleType sentToPoleType)
    {   
        SentByPoleType = sentByPoleType;
        SentToPoleType = sentToPoleType;
        isAccepted = false;
    }

   
}
