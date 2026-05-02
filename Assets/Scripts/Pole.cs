using UnityEngine;

public enum PoleType
{   
    None,
    White,
    Green,
    Red,
    Purple,
    Blue,
    Black
}
public class Pole
{
    public PoleType Type;
    private Switcher Owner;
    private Switcher allowedGuest;
    private bool isCurrentlyOccupied = false;
    private bool isExpectingAGuest = false;
    private bool isHostingAGuest = false;
    private bool isDestroyed = false;
    public Pole(PoleType poletype)
    {
        Type = poletype;
        Owner = null;
        allowedGuest = null;    
        isCurrentlyOccupied = false;
        isDestroyed = false;    
    }

    public void AssignOwner(Switcher owner) { 
        Owner = owner;
        isCurrentlyOccupied = true;
        
    }

    public bool HasOwner()
    {
        return Owner != null;
    }

    public Switcher GetOwner() { 
        return Owner;
    }

    public bool IsCurrentlyOccupied() { 
        return isCurrentlyOccupied;
    }

    public void Occupy()
    {
        isCurrentlyOccupied = true; 
        
    }

    public void Vacate()
    {
        isCurrentlyOccupied = false;
        //allowedGuest = null;
    }

    public void SendOffTheGuest()
    {
        allowedGuest = null;
        isHostingAGuest = false;
    }
    
    public void AllowGuestToComeIn()
    {
        isHostingAGuest = true;
    }
    public bool isThisGuestAllowed(Switcher switcher)
    {
        if (allowedGuest == null) { return false; }
        return switcher.getClientID() == allowedGuest.getClientID();
    }

    public void welcomeGuest(Switcher guest) { 
        allowedGuest = guest;
        isExpectingAGuest = true;
    }

    public bool IsExpectingAGuest()
    {
        return isExpectingAGuest;
    }

    public bool IsPoleReadyToBeSnatched()
    {
        
        return ((!isCurrentlyOccupied || isDestroyed) && !isHostingAGuest);
    }

    public void SnatchPole(Switcher snatcher)
    {
        allowedGuest = snatcher;
        isHostingAGuest=true;
    }

    public void SendOffGuestIfTheyAre(Switcher expectedGuest)
    {
        if (allowedGuest != null &&
            allowedGuest.getClientID() == expectedGuest.getClientID())
        {
            allowedGuest = null;
            isHostingAGuest = false;
        }
        // if allowedGuest is someone else (a snatcher), leave them alone
    }

    public void ChangeOwner(Switcher newOwner) { 
        Owner = newOwner;
        allowedGuest = null;
        isHostingAGuest = false;
        isExpectingAGuest = false;
    }

    public void Abandon()
    {
        Owner = null;
        allowedGuest = null;
        isCurrentlyOccupied = false;
        isHostingAGuest = false;
        isExpectingAGuest = false;
    }
    public void DestroyPole()
    {
        Owner.Kill();
        isDestroyed = true;
    }
    
    public bool IsDestroyed()
    {
        return isDestroyed;
    }
}
