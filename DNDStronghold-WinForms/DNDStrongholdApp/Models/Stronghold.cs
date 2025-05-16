using System;
using System.Collections.Generic;

namespace DNDStrongholdApp.Models
{
    public class Stronghold
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "New Stronghold";
        public string Location { get; set; } = "Unknown";
        public int Level { get; set; } = 1;
        public int Reputation { get; set; } = 0;
        public int CurrentWeek { get; set; } = 1;
        public int YearsSinceFoundation { get; set; } = 0;
        public Season CurrentSeason { get; set; } = Season.Spring;
        public List<string> Owners { get; set; } = new List<string>();
        public int Treasury { get; set; } = 500; // Starting gold
        public List<Building> Buildings { get; set; } = new List<Building>();
        public List<NPC> NPCs { get; set; } = new List<NPC>();
        public List<Resource> Resources { get; set; } = new List<Resource>();
        public List<JournalEntry> Journal { get; set; } = new List<JournalEntry>();
        public List<Mission> ActiveMissions { get; set; } = new List<Mission>();
        public List<Mission> AvailableMissions { get; set; } = new List<Mission>();
        public WeeklyReport? CurrentWeeklyReport { get; set; } = null;

        // Constructor
        public Stronghold()
        {
            // Initialize starting resources
            Resources.Add(new Resource { Type = ResourceType.Gold, Amount = Treasury });
            Resources.Add(new Resource { Type = ResourceType.Food, Amount = 100 });
            Resources.Add(new Resource { Type = ResourceType.Wood, Amount = 50 });
            Resources.Add(new Resource { Type = ResourceType.Stone, Amount = 30 });
            Resources.Add(new Resource { Type = ResourceType.Iron, Amount = 10 });
        }

        // Methods for stronghold management will be added here
        public void AdvanceWeek()
        {
            // Logic for advancing time will be implemented here
            CurrentWeek++;
            
            // Change season every 13 weeks (approximately)
            if (CurrentWeek % 13 == 0)
            {
                CurrentSeason = (Season)(((int)CurrentSeason + 1) % 4);
                
                // If it's spring again, increment year
                if (CurrentSeason == Season.Spring)
                {
                    YearsSinceFoundation++;
                }
            }
            
            // Additional logic for resource production, building progress, etc. will be added
        }
    }

    public enum Season
    {
        Spring,
        Summer,
        Fall,
        Winter
    }
} 