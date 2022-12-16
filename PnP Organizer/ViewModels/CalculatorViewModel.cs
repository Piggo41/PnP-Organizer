﻿using CommunityToolkit.Mvvm.ComponentModel;
using PnP_Organizer.Core.BattleAssistant;
using PnP_Organizer.Core.Character.Inventory;
using PnP_Organizer.Core.Character;
using PnP_Organizer.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Wpf.Ui.Common.Interfaces;
using Wpf.Ui.Mvvm.Contracts;
using PnP_Organizer.IO;
using PnP_Organizer.Views.Pages;
using PnP_Organizer.Core.Character.SkillSystem;
using System.Collections.Generic;
using System;
using CommunityToolkit.Mvvm.Input;
using PnP_Organizer.Properties;

namespace PnP_Organizer.ViewModels
{
    public partial class CalculatorViewModel : ObservableObject, INavigationAware
    {
        public List<LocalizedBattleAction> BattleActions { get; }

        [ObservableProperty]
        private BattleTurn? _currentTurn;
        [ObservableProperty]
        private int _currentTurnCount = 0;

        [ObservableProperty]
        private ObservableCollection<ItemSelectorModel> _itemSelectorModels = new();

        [ObservableProperty]
        private InventoryWeapon? _selectedWeapon;
        [ObservableProperty]
        private InventoryArmor? _selectedArmor;
        [ObservableProperty]
        private InventoryShield? _selectedShield;

        [ObservableProperty]
        private ObservableCollection<Skill> _passiveSkills = new();
        [ObservableProperty]
        private ObservableCollection<Skill> _activeSkills = new();

        [ObservableProperty]
        private ObservableCollection<CalculatorSkillModel> _calculatorSkillModels = new();

        [ObservableProperty]
        private LocalizedBattleAction _action;

        [ObservableProperty]
        private int _incomingDamage;

        [ObservableProperty]
        private int _health = 0;
        [ObservableProperty]
        private int _energy = 0;
        [ObservableProperty]
        private int _stamina = 0;
        [ObservableProperty]
        private int _initiative = 0;

        [ObservableProperty]
        private int _healthDiff = 0;
        [ObservableProperty]
        private int _energyDiff = 0;
        [ObservableProperty]
        private int _staminaDiff = 0;

        [ObservableProperty]
        private int _damage = 0;
        [ObservableProperty]
        private int _hit = 0;
        [ObservableProperty]
        private int _armorpen = 0;
        [ObservableProperty]
        private int _armor = 0;
        [ObservableProperty]
        private int _parade = 0;
        [ObservableProperty]
        private int _dodge = 0;

        [ObservableProperty]
        private TurnPhase _turnPhase = TurnPhase.PreTurn;
        [ObservableProperty]
        private BattlePhase _battlePhase = BattlePhase.BetweenBattles;

        private readonly IPageService _pageService;

        public CalculatorViewModel(IPageService pageService)
        {
            _pageService = pageService;
            BattleActions = Enum.GetValues<BattleAction>().ToList()
                .ConvertAll(battleAction => new LocalizedBattleAction(battleAction, Resources.ResourceManager.GetString($"Calculator_BattleAction{battleAction}")!));
            PropertyChanged += CalculatorViewModel_PropertyChanged;
        }

        public void OnNavigatedTo()
        {
            if (BattlePhase == BattlePhase.BetweenBattles)
                LoadCharacterStats();
        }

        public void OnNavigatedFrom() 
        {
            SaveCharacterStats();
        }

        [RelayCommand]
        public void AbortBattle()
        {
            BattlePhase = BattlePhase.BetweenBattles;

            LoadCharacterStats();

            CurrentTurn = null;

            ItemSelectorModels?.Clear();
            CalculatorSkillModels?.Clear();
        }

        [RelayCommand]
        private void StartNewBattle()
        {
            BattlePhase = BattlePhase.InBattle;
            foreach (var passiveSkill in PassiveSkills)
            {
                passiveSkill.UsesLeft = passiveSkill.UsesPerBattle;
            }
            foreach (var activeSkill in ActiveSkills)
            {
                activeSkill.UsesLeft = activeSkill.UsesPerBattle;
            }
            CurrentTurnCount = -1; // NewTurn() increases the CurrentTurnCount by one, so the start count must be one less than 0

            LoadItems();
            UpdateSkillsList();
            NewTurn();
        }

        [RelayCommand]
        private void RestartBattle()
        {
            AbortBattle();
            StartNewBattle();
        }

        [RelayCommand]
        private void EndBattle()
        {
            EndTurn();
            BattlePhase = BattlePhase.BetweenBattles;
        }

        [RelayCommand]
        private void ManageTurn()
        {
            if (TurnPhase == TurnPhase.PreTurn)
                EndTurn();
            else if (TurnPhase == TurnPhase.PostTurn)
                NewTurn();
        }

        private void CalculatorViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(SelectedWeapon) or nameof(SelectedArmor) or nameof(SelectedShield)
                or nameof(Action) or nameof(PassiveSkills) or nameof(ActiveSkills))
            {
                PopulateCalculatorSkillModels();
            }
        }

        private void PopulateCalculatorSkillModels()
        {
            var validWeaponSkills = ActiveSkills.Where(skill =>
            {
                if (SelectedWeapon != null)
                {
                    if (SelectedWeapon.AttackMode == AttackMode.Ranged)
                        return skill.SkillCategory == SkillCategory.Ranged || skill.SkillCategory == SkillCategory.Character;

                    return skill.SkillCategory == SkillCategory.Melee || skill.SkillCategory == SkillCategory.Character;
                }
                return skill.SkillCategory == SkillCategory.Character;
            });

            // TODO Add checks for armor and shield specific skills

            CalculatorSkillModels = new ObservableCollection<CalculatorSkillModel>(validWeaponSkills.ToList().ConvertAll(skill => new CalculatorSkillModel(skill)));
        }

        private void EndTurn()
        {
            var usedSkills = CalculatorSkillModels.Where(calcSkillModel => calcSkillModel.IsActive && calcSkillModel.Skill.UsesLeft > 0)
                .ToList().ConvertAll(calcSkillModel => calcSkillModel.Skill).Concat(PassiveSkills);

            var activatedPassiveSkills = PassiveSkills.Where(skill => IsSkillUsable(skill));

            //usedSkills.ToList().AddRange()

            CurrentTurn = new BattleTurn(_pageService, SelectedWeapon, SelectedArmor, SelectedShield, usedSkills.ToList(),
                Action.BattleAction, Health, Energy, Stamina, Initiative, IncomingDamage);

            foreach (var skill in usedSkills)
            {
                if (skill.UsesPerBattle > 0)
                    skill.UsesLeft--;
            }

            Health = CurrentTurn.HealthAfter;
            Energy = CurrentTurn.EnergyAfter;
            Stamina = CurrentTurn.StaminaAfter;
            Initiative = CurrentTurn.ModifiedInitiative;

            HealthDiff = Health - CurrentTurn.HealthBefore;
            EnergyDiff = Energy - CurrentTurn.EnergyBefore;
            StaminaDiff = Stamina - CurrentTurn.StaminaBefore;

            if (CurrentTurn.Action == BattleAction.Attack)
            {
                Damage = CurrentTurn.Damage;
                Hit = CurrentTurn.Hit;
                Armorpen = CurrentTurn.Armorpen;
            }
            else if (CurrentTurn.Action == BattleAction.Defend)
            {
                Armor = CurrentTurn.Armor;
                Dodge = CurrentTurn.Dodge;
                Parade = CurrentTurn.Parade;
            }

            TurnPhase = TurnPhase.PostTurn;
        }

        private void NewTurn()
        {
            CurrentTurnCount++;
            TurnPhase = TurnPhase.PreTurn;
        }

        private void LoadCharacterStats()
        {
            var character = FileIO.LoadedCharacter;

            Health = character.CurrentHealth;
            Energy = character.CurrentEnergy;
            Stamina = character.CurrentStamina;
            Initiative = _pageService.GetPage<OverviewPage>()!.ViewModel!.Initiative + character.InitiativeBonus;
        }

        private void SaveCharacterStats()
        {
            FileIO.LoadedCharacter.CurrentHealth = Health;
            FileIO.LoadedCharacter.CurrentEnergy = Energy;
            FileIO.LoadedCharacter.CurrentStamina = Stamina;
        }

        private void UpdateSkillsList()
        {
            var skillModels = _pageService.GetPage<SkillsPage>()!.ViewModel!.SkillModels;
            var skilledSkills = skillModels!.Where(skillModel => skillModel.IsActive && skillModel is not RepeatableSkillModel)
                .ToList().ConvertAll(skillModel => skillModel.Skill!);
            PassiveSkills = new ObservableCollection<Skill>(skilledSkills!.Where(skill => skill.ActivationType == ActivationType.Passive));
            ActiveSkills = new ObservableCollection<Skill>(skilledSkills!.Where(skill => skill.ActivationType == ActivationType.Active));

            PopulateCalculatorSkillModels();
        }

        private void LoadItems()
        {
            var inventoryItems = _pageService.GetPage<InventoryPage>()!.ViewModel!.Items?
                .Where(itemModel => itemModel is InventoryWeaponModel or InventoryArmorModel or InventoryShieldModel)
                .ToList().ConvertAll(itemModel => itemModel.InventoryItem);

            if (inventoryItems != null)
            {
                ItemSelectorModels.Clear();

                var weapons = inventoryItems!.Where(item => item is InventoryWeapon).Cast<InventoryWeapon>();
                if (weapons.Any())
                    ItemSelectorModels.Add(new ItemSelectorModel(weapons));

                var armors = inventoryItems!.Where(item => item is InventoryArmor).Cast<InventoryArmor>();
                if (armors.Any())
                    ItemSelectorModels.Add(new ItemSelectorModel(armors));

                var shields = inventoryItems!.Where(item => item is InventoryShield).Cast<InventoryShield>();
                if (shields.Any())
                    ItemSelectorModels.Add(new ItemSelectorModel(shields));
            }
        }

        private bool IsSkillUsable(Skill skill)
        {
            var action = Action.BattleAction;
            var isUsable = true;
            if(action == BattleAction.Attack)
            {
                var usableWithWeapon = true;
                if(SelectedWeapon != null)
                {
                    if (SelectedWeapon.AttackMode == AttackMode.Melee)
                    {
                        if (skill.Name == Skills.Instance.OneHandedCombat.Name)
                            usableWithWeapon = !SelectedWeapon.IsTwoHanded;
                        else
                            usableWithWeapon = skill.SkillCategory == SkillCategory.Melee;
                    }
                    else
                        usableWithWeapon = skill.SkillCategory == SkillCategory.Ranged && skill.SkillCategory == SkillCategory.Ranged;
                }
                var usableWithShield = true;
                if (SelectedShield != null)
                {
                    if (skill.Name == Skills.Instance.ShieldBash.Name || skill.Name == Skills.Instance.SomethingWithShield.Name)
                        usableWithShield = action == BattleAction.Attack;
                    else
                        usableWithShield = action == BattleAction.Defend;
                }
                isUsable = usableWithWeapon || usableWithShield;
            }
            else if(action == BattleAction.Defend) 
            { 
                // TODO
            }

            return isUsable;
        }

        
    }

    public enum TurnPhase
    {
        PreTurn,
        PostTurn
    }

    public enum BattlePhase
    {
        BetweenBattles,
        InBattle
    }

    public struct LocalizedBattleAction
    {
        public BattleAction BattleAction { get; set; }
        public string LocalizedName { get; set; }

        public LocalizedBattleAction(BattleAction battleAction, string localizedName)
        {
            BattleAction = battleAction;
            LocalizedName = localizedName;
        }
    }
}
