using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KamatekCrm.Components
{
    /// <summary>
    /// KmWizardStepper — Çok adımlı form wizard.
    /// Adım başlıkları, progress bar, ileri/geri butonları, adım validasyonu.
    /// Kullanım: Yeni müşteri oluşturma, proje teklifi, iş emri wizard'ı
    /// </summary>
    public class KmWizardStepper : Control
    {
        static KmWizardStepper()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(KmWizardStepper),
                new FrameworkPropertyMetadata(typeof(KmWizardStepper)));
        }

        #region Dependency Properties

        public static readonly DependencyProperty StepsProperty = DependencyProperty.Register(
            nameof(Steps), typeof(System.Collections.ObjectModel.ObservableCollection<WizardStep>), typeof(KmWizardStepper),
            new PropertyMetadata(null));

        public static readonly DependencyProperty CurrentStepIndexProperty = DependencyProperty.Register(
            nameof(CurrentStepIndex), typeof(int), typeof(KmWizardStepper),
            new PropertyMetadata(0, OnCurrentStepChanged));

        public static readonly DependencyProperty TotalStepsProperty = DependencyProperty.Register(
            nameof(TotalSteps), typeof(int), typeof(KmWizardStepper),
            new PropertyMetadata(0));

        public static readonly DependencyProperty ProgressPercentProperty = DependencyProperty.Register(
            nameof(ProgressPercent), typeof(double), typeof(KmWizardStepper),
            new PropertyMetadata(0.0));

        public static readonly DependencyProperty CanGoNextProperty = DependencyProperty.Register(
            nameof(CanGoNext), typeof(bool), typeof(KmWizardStepper),
            new PropertyMetadata(true));

        public static readonly DependencyProperty CanGoPreviousProperty = DependencyProperty.Register(
            nameof(CanGoPrevious), typeof(bool), typeof(KmWizardStepper),
            new PropertyMetadata(false));

        public static readonly DependencyProperty IsLastStepProperty = DependencyProperty.Register(
            nameof(IsLastStep), typeof(bool), typeof(KmWizardStepper),
            new PropertyMetadata(false));

        public static readonly DependencyProperty NextCommandProperty = DependencyProperty.Register(
            nameof(NextCommand), typeof(ICommand), typeof(KmWizardStepper));

        public static readonly DependencyProperty PreviousCommandProperty = DependencyProperty.Register(
            nameof(PreviousCommand), typeof(ICommand), typeof(KmWizardStepper));

        public static readonly DependencyProperty FinishCommandProperty = DependencyProperty.Register(
            nameof(FinishCommand), typeof(ICommand), typeof(KmWizardStepper));

        public static readonly DependencyProperty CurrentStepTitleProperty = DependencyProperty.Register(
            nameof(CurrentStepTitle), typeof(string), typeof(KmWizardStepper),
            new PropertyMetadata(""));

        #endregion

        #region Properties

        public System.Collections.ObjectModel.ObservableCollection<WizardStep> Steps
        {
            get => (System.Collections.ObjectModel.ObservableCollection<WizardStep>)GetValue(StepsProperty);
            set => SetValue(StepsProperty, value);
        }

        public int CurrentStepIndex
        {
            get => (int)GetValue(CurrentStepIndexProperty);
            set => SetValue(CurrentStepIndexProperty, value);
        }

        public int TotalSteps
        {
            get => (int)GetValue(TotalStepsProperty);
            set => SetValue(TotalStepsProperty, value);
        }

        public double ProgressPercent
        {
            get => (double)GetValue(ProgressPercentProperty);
            set => SetValue(ProgressPercentProperty, value);
        }

        public bool CanGoNext
        {
            get => (bool)GetValue(CanGoNextProperty);
            set => SetValue(CanGoNextProperty, value);
        }

        public bool CanGoPrevious
        {
            get => (bool)GetValue(CanGoPreviousProperty);
            set => SetValue(CanGoPreviousProperty, value);
        }

        public bool IsLastStep
        {
            get => (bool)GetValue(IsLastStepProperty);
            set => SetValue(IsLastStepProperty, value);
        }

        public ICommand? NextCommand
        {
            get => (ICommand?)GetValue(NextCommandProperty);
            set => SetValue(NextCommandProperty, value);
        }

        public ICommand? PreviousCommand
        {
            get => (ICommand?)GetValue(PreviousCommandProperty);
            set => SetValue(PreviousCommandProperty, value);
        }

        public ICommand? FinishCommand
        {
            get => (ICommand?)GetValue(FinishCommandProperty);
            set => SetValue(FinishCommandProperty, value);
        }

        public string CurrentStepTitle
        {
            get => (string)GetValue(CurrentStepTitleProperty);
            set => SetValue(CurrentStepTitleProperty, value);
        }

        #endregion

        public KmWizardStepper()
        {
            Steps = new System.Collections.ObjectModel.ObservableCollection<WizardStep>();
        }

        private static void OnCurrentStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KmWizardStepper wizard)
                wizard.UpdateStepState();
        }

        private void UpdateStepState()
        {
            TotalSteps = Steps.Count;
            if (TotalSteps == 0) return;

            CanGoPrevious = CurrentStepIndex > 0;
            CanGoNext = CurrentStepIndex < TotalSteps - 1;
            IsLastStep = CurrentStepIndex == TotalSteps - 1;
            ProgressPercent = TotalSteps > 1 ? (double)CurrentStepIndex / (TotalSteps - 1) * 100 : 100;

            if (CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count)
            {
                CurrentStepTitle = Steps[CurrentStepIndex].Title;

                // Update each step's status
                for (int i = 0; i < Steps.Count; i++)
                {
                    Steps[i].Status = i < CurrentStepIndex ? WizardStepStatus.Completed
                                    : i == CurrentStepIndex ? WizardStepStatus.Active
                                    : WizardStepStatus.Pending;
                }
            }
        }

        public void GoNext()
        {
            if (CanGoNext) CurrentStepIndex++;
        }

        public void GoPrevious()
        {
            if (CanGoPrevious) CurrentStepIndex--;
        }
    }

    public class WizardStep
    {
        public string Title { get; set; } = "";
        public string? Icon { get; set; }
        public string? Description { get; set; }
        public WizardStepStatus Status { get; set; } = WizardStepStatus.Pending;

        public string StatusIcon => Status switch
        {
            WizardStepStatus.Completed => "✅",
            WizardStepStatus.Active => "🔵",
            WizardStepStatus.Error => "❌",
            _ => "⬜"
        };
    }

    public enum WizardStepStatus
    {
        Pending,
        Active,
        Completed,
        Error
    }
}
