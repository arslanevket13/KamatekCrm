using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using GongSolutions.Wpf.DragDrop;
using KamatekCrm.Data;
using KamatekCrm.Enums;
using KamatekCrm.Models;
using Microsoft.EntityFrameworkCore;

namespace KamatekCrm.ViewModels
{
    /// <summary>
    /// Kanban Satış Boru Hattı ViewModel
    /// GongSolutions.WPF.DragDrop implementasyonu
    /// </summary>
    public class PipelineViewModel : ViewModelBase, IDropTarget
    {
        private readonly AppDbContext _context;

        #region Kanban Columns

        public ObservableCollection<ServiceProject> Leads { get; } = new();
        public ObservableCollection<ServiceProject> Quoted { get; } = new();
        public ObservableCollection<ServiceProject> Negotiating { get; } = new();
        public ObservableCollection<ServiceProject> Won { get; } = new();
        public ObservableCollection<ServiceProject> Lost { get; } = new();

        #endregion

        public PipelineViewModel()
        {
            _context = new AppDbContext();
            LoadData();
        }

        private void LoadData()
        {
            Leads.Clear();
            Quoted.Clear();
            Negotiating.Clear();
            Won.Clear();
            Lost.Clear();

            var projects = _context.ServiceProjects
                .Include(p => p.Customer)
                .Where(p => p.Status != ProjectStatus.Cancelled) // İptal edilenler hariç
                .ToList();

            foreach (var p in projects)
            {
                switch (p.PipelineStage)
                {
                    case PipelineStage.Lead: Leads.Add(p); break;
                    case PipelineStage.Quoted: Quoted.Add(p); break;
                    case PipelineStage.Negotiating: Negotiating.Add(p); break;
                    case PipelineStage.Won: Won.Add(p); break;
                    case PipelineStage.Lost: Lost.Add(p); break;
                }
            }
        }

        #region IDropTarget Implementation

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ServiceProject && dropInfo.TargetCollection is ObservableCollection<ServiceProject>)
            {
                dropInfo.Effects = DragDropEffects.Move;
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ServiceProject project)
            {
                var sourceCollection = dropInfo.DragInfo.SourceCollection as ObservableCollection<ServiceProject>;
                var targetCollection = dropInfo.TargetCollection as ObservableCollection<ServiceProject>;

                if (targetCollection == null || sourceCollection == null) return;

                // UI Update
                sourceCollection.Remove(project);
                targetCollection.Insert(dropInfo.InsertIndex, project);

                // Database Update logic...
                // Hedef koleksiyona göre PipelineStage'i belirle
                PipelineStage newStage;

                if (targetCollection == Leads) newStage = PipelineStage.Lead;
                else if (targetCollection == Quoted) newStage = PipelineStage.Quoted;
                else if (targetCollection == Negotiating) newStage = PipelineStage.Negotiating;
                else if (targetCollection == Won) newStage = PipelineStage.Won;
                else if (targetCollection == Lost) newStage = PipelineStage.Lost;
                else return; // Hata

                UpdateProjectStage(project, newStage);
            }
        }

        private void UpdateProjectStage(ServiceProject project, PipelineStage newStage)
        {
            try
            {
                var dbProject = _context.ServiceProjects.Find(project.Id);
                if (dbProject != null)
                {
                    dbProject.PipelineStage = newStage;
                    
                    // Won aşamasına geçişte özel işlem: İş Emri Oluşturma Onayı
                    if (newStage == PipelineStage.Won)
                    {
                        var result = MessageBox.Show(
                            $"'{calculateDesc(project)}' satışını kazandınız! \nBu proje için otomatik iş emri oluşturulsun mu?",
                            "Satış Kazanıldı 🚀",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            CreateJobForWonProject(dbProject);
                        }
                    }

                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}");
                LoadData(); // Hata durumunda geri al
            }
        }

        private string calculateDesc(ServiceProject p)
        {
            return p.Title;
        }

        private void CreateJobForWonProject(ServiceProject project)
        {
            // Otomatik Proje İş Emri
            var job = new ServiceJob
            {
                CustomerId = project.CustomerId,
                ServiceProjectId = project.Id,
                ServiceJobType = ServiceJobType.Project,
                JobCategory = JobCategory.Other, // Detaya göre seçilebilir
                WorkOrderType = WorkOrderType.Installation,
                Description = $"Proje Başlangıcı: {project.Title}",
                Status = JobStatus.Pending,
                CreatedDate = DateTime.Now,
                Price = project.TotalCost // veya Bütçe
            };

            _context.ServiceJobs.Add(job);
            // Project status da Active yapılabilir
            project.Status = ProjectStatus.Active;
        }

        #endregion
    }
}
