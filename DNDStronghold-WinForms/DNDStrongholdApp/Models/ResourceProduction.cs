using System;
using System.Collections.Generic;
using DNDStrongholdApp.Models;


public class ResourceProduction
{
    public ResourceType ResourceType { get; set; }
    public int Amount { get; set; }  // Rounded down value for game mechanics
    public decimal ExactAmount { get; set; }  // Exact value including decimals for display
} 