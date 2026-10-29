using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using KamatekCrm.ApplicationCore.Common;
using KamatekCrm.ApplicationCore.DTOs.CustomerInteractions;
using KamatekCrm.ApplicationCore.DTOs.Users;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Events;
using KamatekCrm.Infrastructure.Data;
using KamatekCrm.Infrastructure.Services;
using KamatekCrm.Services;
using KamatekCrm.Shared.Enums;
using KamatekCrm.Shared.Models;
using KamatekCrm.ViewModels;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KamatekCrm.Tests.Services
{
    public class InteractionsAndAgendaTests
    {
        private (AppDbContext Context, Mock<IDbContextFactory<AppDbContext>> FactoryMock, CustomerInteractionReadService ReadService, CustomerInteractionCommandService CommandService)
            CreateServices(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            var context = new AppDbContext(options);

            var factoryMock = new Mock<IDbContextFactory<AppDbContext>>();
            factoryMock.Setup(f => f.CreateDbContextAsync(default))
                .ReturnsAsync(() => new AppDbContext(options));

            var authMock = new Mock<IApplicationAuthorizationService>();
            authMock.Setup(a => a.Authorize(It.IsAny<ApplicationPermission>()))
                .Returns(Result.Success());

            var currentUserMock = new Mock<ICurrentUserContext>();
            currentUserMock.Setup(c => c.UserId).Returns(1);
            currentUserMock.Setup(c => c.Username).Returns("admin");

            var readService = new CustomerInteractionReadService(factoryMock.Object);
            var commandService = new CustomerInteractionCommandService(factoryMock.Object, authMock.Object, currentUserMock.Object);

            return (context, factoryMock, readService, commandService);
        }

        /// <summary>
        /// TEST 1 (Overdue Rollover):
        /// Geçmiş tarihli (FollowUpDate = Yesterday) ve tamamlanmamış bir iletişim/takip kaydı
        /// bugünün Yönetici Gündeminde IsOverdue = true olarak işaretlenmeli ve öncelik puanı artmalı;
        /// tamamlanmış (Status = Completed) geçmiş kayıtlar ise gecikmiş/gündem listesine düşmemelidir.
        /// </summary>
        [Fact]
        public async Task Test1_OverdueRollover_IncompletePastRecord_IsMarkedOverdue_And_CompletedPastRecordExcluded()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var (context, _, readService, _) = CreateServices(dbName);
            var nowUtc = DateTime.UtcNow;

            // 1. Dünden kalan ve tamamlanmamış takip kaydı
            var overdueInteraction = new CustomerInteraction
            {
                InteractionNumber = "TALEP-OVERDUE-001",
                CallerName = "Ahmet Yılmaz",
                CallerPhone = "05321112233",
                NormalizedPhone = "+905321112233",
                Subject = "Teklif Geri Dönüşü Bekleniyor",
                Summary = "Dün aranacaktı, görüşülemedi.",
                RequestType = InteractionRequestType.CallBack,
                Priority = InteractionPriority.High,
                Status = InteractionStatus.Scheduled,
                RequiresFollowUp = true,
                FollowUpDate = nowUtc.AddDays(-1), // Dün
                InteractionDate = nowUtc.AddDays(-2),
                IsActive = true
            };

            // 2. Geçmişte tamamlanmış takip kaydı
            var completedPastInteraction = new CustomerInteraction
            {
                InteractionNumber = "TALEP-COMPLETED-002",
                CallerName = "Mehmet Demir",
                CallerPhone = "05423334455",
                NormalizedPhone = "+905423334455",
                Subject = "Eski Tamamlanmış Görüşme",
                Summary = "2 gün önce çözüldü.",
                RequestType = InteractionRequestType.PriceQuote,
                Priority = InteractionPriority.Normal,
                Status = InteractionStatus.Completed,
                RequiresFollowUp = true,
                FollowUpDate = nowUtc.AddDays(-2),
                CompletedDate = nowUtc.AddDays(-1),
                InteractionDate = nowUtc.AddDays(-3),
                IsActive = true
            };

            // 3. Gelecek tarihli normal takip kaydı
            var futureInteraction = new CustomerInteraction
            {
                InteractionNumber = "TALEP-FUTURE-003",
                CallerName = "Can Aksoy",
                CallerPhone = "05556667788",
                NormalizedPhone = "+905556667788",
                Subject = "Haftaya Keşif Randevusu",
                Summary = "3 gün sonra keşif için aranacak.",
                RequestType = InteractionRequestType.Discovery,
                Priority = InteractionPriority.Normal,
                Status = InteractionStatus.Scheduled,
                RequiresFollowUp = true,
                FollowUpDate = nowUtc.AddDays(3), // 3 gün sonra
                InteractionDate = nowUtc,
                IsActive = true
            };

            context.CustomerInteractions.AddRange(overdueInteraction, completedPastInteraction, futureInteraction);
            await context.SaveChangesAsync();

            // Act: Yönetici Gündemini çek
            var agendaResult = await readService.GetManagerAgendaAsync();

            // Assert
            agendaResult.IsSuccess.Should().BeTrue();
            agendaResult.Value.Should().NotBeNull();
            var agendaItems = agendaResult.Value!;

            // 1. Dünden kalan tamamlanmamış kayıt gündemde yer almalı
            var overdueItem = agendaItems.FirstOrDefault(x => x.InteractionNumber == "TALEP-OVERDUE-001");
            overdueItem.Should().NotBeNull();
            overdueItem!.IsOverdue.Should().BeTrue("Dünden kalan tamamlanmamış kayıt IsOverdue = true olmalıdır.");
            overdueItem.OverdueText.Should().NotBeNullOrEmpty("Gecikme süresi metni doldurulmalıdır.");
            overdueItem.OverdueText.Should().Contain("gün gecikti");
            overdueItem.PriorityScore.Should().BeGreaterThan(150, "Gecikmiş kaydın öncelik puanı gecikme cezasıyla yükseltilmelidir.");

            // 2. Tamamlanmış geçmiş kayıt gündeme düşmemelidir
            var completedItem = agendaItems.FirstOrDefault(x => x.InteractionNumber == "TALEP-COMPLETED-002");
            completedItem.Should().BeNull("Tamamlanmış geçmiş kayıtlar yönetici gündeminde yer almamalıdır.");

            // 3. Gelecek kayıt gündemde yer alabilir ancak puanı gecikmiş olandan düşük olmalıdır
            var futureItem = agendaItems.FirstOrDefault(x => x.InteractionNumber == "TALEP-FUTURE-003");
            if (futureItem != null)
            {
                futureItem.IsOverdue.Should().BeFalse();
                overdueItem.PriorityScore.Should().BeGreaterThan(futureItem.PriorityScore);
            }
        }

        /// <summary>
        /// TEST 2 (Priority Scoring & Sorting):
        /// SLA ihlali olan iş emri, gecikmiş müşteri araması ve normal ileri tarihli görevin
        /// birleşik gündemde doğru aciliyet sırasıyla (Kritik -> Yüksek -> Normal) sıralanması.
        /// </summary>
        [Fact]
        public async Task Test2_PriorityScoringAndSorting_OrdersCorrectly_By_Urgency_And_Score()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var (context, _, readService, _) = CreateServices(dbName);
            var nowUtc = DateTime.UtcNow;

            var customer = new Customer
            {
                Id = 101,
                CustomerCode = "CUST-101",
                FullName = "Tekno Plaza A.Ş.",
                PhoneNumber = "02123334455",
                City = "İstanbul"
            };
            context.Customers.Add(customer);

            // a) SLA İhlali olan Servis İş Emri (En yüksek aciliyet)
            var breachedJob = new ServiceJob
            {
                Id = 501,
                CustomerId = customer.Id,
                Customer = customer,
                Title = "Yangın Santral Alarmı",
                JobCategory = JobCategory.FireAlarm,
                Description = "Ana yangın santrali alarm veriyor",
                Status = JobStatus.Pending,
                CreatedDate = nowUtc.AddDays(-1),
                SlaDeadline = nowUtc.AddHours(-4) // 4 saat önce SLA süresi doldu!
            };

            // b) Gecikmiş Müşteri Geri Dönüş Talebi (Orta-Yüksek aciliyet)
            var overdueInteraction = new CustomerInteraction
            {
                InteractionNumber = "TALEP-OVERDUE-002",
                CustomerId = customer.Id,
                CustomerName = customer.FullName,
                CallerName = "Burak Kaya",
                CallerPhone = "05332221100",
                NormalizedPhone = "+905332221100",
                Subject = "Fiyat Revizesi İstendi",
                Summary = "Yangın kapısı revize teklifi bekliyor.",
                RequestType = InteractionRequestType.PriceQuote,
                Priority = InteractionPriority.High,
                Status = InteractionStatus.Scheduled,
                RequiresFollowUp = true,
                FollowUpDate = nowUtc.AddHours(-2), // 2 saat önce aranmalıydı
                InteractionDate = nowUtc.AddDays(-1),
                IsActive = true
            };

            // c) İleri Tarihli Normal Görev (Normal aciliyet)
            var normalFutureInteraction = new CustomerInteraction
            {
                InteractionNumber = "TALEP-NORMAL-003",
                CustomerId = customer.Id,
                CustomerName = customer.FullName,
                CallerName = "Burak Kaya",
                CallerPhone = "05332221100",
                NormalizedPhone = "+905332221100",
                Subject = "Rutin Bakım Bilgilendirmesi",
                Summary = "Gelecek hafta genel bakım takvimi iletilecek.",
                RequestType = InteractionRequestType.ServiceStatus,
                Priority = InteractionPriority.Normal,
                Status = InteractionStatus.Scheduled,
                RequiresFollowUp = true,
                FollowUpDate = nowUtc.AddDays(5), // 5 gün sonra
                InteractionDate = nowUtc,
                IsActive = true
            };

            context.ServiceJobs.Add(breachedJob);
            context.CustomerInteractions.AddRange(overdueInteraction, normalFutureInteraction);
            await context.SaveChangesAsync();

            // Act
            var result = await readService.GetManagerAgendaAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            var items = result.Value!;
            items.Count.Should().BeGreaterOrEqualTo(3);

            // Öncelik puanına göre sıralandığını doğrula
            for (int i = 0; i < items.Count - 1; i++)
            {
                items[i].PriorityScore.Should().BeGreaterOrEqualTo(items[i + 1].PriorityScore,
                    $"Gündem maddesi {i} (Puan: {items[i].PriorityScore}), madde {i + 1}'den (Puan: {items[i + 1].PriorityScore}) daha yüksek veya eşit puanlı olmalıdır.");
            }

            // 1. Sıra: SLA İhlali olan İş Emri (Score >= 250, PriorityLevel = Critical)
            var topItem = items.First();
            topItem.SourceType.Should().Be("ServiceJob");
            topItem.PriorityLevel.Should().Be("Critical");
            topItem.PriorityScore.Should().BeGreaterOrEqualTo(250);

            // 2. Sıra: Gecikmiş etkileşim (Overdue, PriorityScore >= 160)
            var secondItem = items.FirstOrDefault(x => x.InteractionNumber == "TALEP-OVERDUE-002");
            secondItem.Should().NotBeNull();
            secondItem!.IsOverdue.Should().BeTrue();
            secondItem.PriorityScore.Should().BeLessThan(topItem.PriorityScore);

            // 3. Sıra: Gelecek normal görev
            var normalItem = items.FirstOrDefault(x => x.InteractionNumber == "TALEP-NORMAL-003");
            normalItem.Should().NotBeNull();
            normalItem!.PriorityLevel.Should().Be("Normal");
            normalItem.PriorityScore.Should().BeLessThan(secondItem.PriorityScore);
        }

        /// <summary>
        /// TEST 3 (Snooze & Complete Actions):
        /// Bir gündem maddesi ertelendiğinde (Snooze/Reschedule) yeni takip tarihinin ve geçmişinin doğru güncellenmesi;
        /// tamamlandığında (Complete) ise CompletedDate alanının dolup aktif gecikmiş listesinden düşmesi.
        /// </summary>
        [Fact]
        public async Task Test3_SnoozeAndCompleteActions_UpdatesFollowUpDate_And_RemovesFromAgenda()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var (context, _, readService, commandService) = CreateServices(dbName);
            var nowUtc = DateTime.UtcNow;

            var interaction = new CustomerInteraction
            {
                InteractionNumber = "TALEP-ACTION-001",
                CallerName = "Zeynep Aydın",
                CallerPhone = "05334445566",
                NormalizedPhone = "+905334445566",
                Subject = "Kamera Sistemi Teklifi",
                Summary = "Fiyat teklifi sunuldu, karar bekleniyor.",
                RequestType = InteractionRequestType.PriceQuote,
                Priority = InteractionPriority.High,
                Status = InteractionStatus.Scheduled,
                RequiresFollowUp = true,
                FollowUpDate = nowUtc.AddDays(-1), // Dün (Gecikmiş)
                InteractionDate = nowUtc.AddDays(-2),
                IsActive = true
            };

            context.CustomerInteractions.Add(interaction);
            await context.SaveChangesAsync();

            // 1. Başlangıçta kayıt gecikmiş takip listesinde olmalı
            var initialOverdue = await readService.GetOverdueFollowUpsAsync();
            initialOverdue.IsSuccess.Should().BeTrue();
            initialOverdue.Value.Should().NotBeNull();
            initialOverdue.Value!.Should().Contain(x => x.Id == interaction.Id);

            // Act 1: Erteleme (Snooze) - Takip tarihini 2 gün sonraya ötele
            var newFollowUpUtc = DateTime.SpecifyKind(nowUtc.AddDays(2), DateTimeKind.Utc);
            var snoozeResult = await commandService.RescheduleAsync(
                interaction.Id,
                newFollowUpUtc,
                "Müşteri yurt dışından dönünce aranmak istedi.");

            // Assert 1: Erteleme başarılı ve tarih güncellendi
            snoozeResult.IsSuccess.Should().BeTrue();

            await using (var verifyContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options))
            {
                var updated = await verifyContext.CustomerInteractions.FindAsync(interaction.Id);
                updated.Should().NotBeNull();
                updated!.FollowUpDate.Should().BeCloseTo(newFollowUpUtc, TimeSpan.FromSeconds(2));
                updated.Status.Should().Be(InteractionStatus.Scheduled);

                // Geçmiş tablosuna erteleme kaydı eklenmiş olmalı
                var history = await verifyContext.CustomerInteractionHistories
                    .Where(h => h.CustomerInteractionId == interaction.Id)
                    .OrderByDescending(h => h.ChangedAt)
                    .FirstOrDefaultAsync();

                history.Should().NotBeNull();
                history!.Reason.Should().Contain("Müşteri yurt dışından dönünce aranmak istedi");
            }

            // Gecikmiş listesinden artık düşmüş olmalı (çünkü tarihi 2 gün sonraya ertelendi)
            var postSnoozeOverdue = await readService.GetOverdueFollowUpsAsync();
            postSnoozeOverdue.Value.Should().NotBeNull();
            postSnoozeOverdue.Value!.Should().NotContain(x => x.Id == interaction.Id);

            // Act 2: Tamamlama (Complete)
            var completeDto = new UpdateCustomerInteractionStatusDto
            {
                InteractionId = interaction.Id,
                NewStatus = InteractionStatus.Completed,
                ResolutionNotes = "Müşteri ile görüşüldü, teklif onaylandı.",
                Reason = "Görev tamamlandı."
            };
            var completeResult = await commandService.UpdateStatusAsync(completeDto);

            // Assert 2: Tamamlama başarılı ve CompletedDate dolduruldu
            completeResult.IsSuccess.Should().BeTrue();

            await using (var verifyContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options))
            {
                var completed = await verifyContext.CustomerInteractions.FindAsync(interaction.Id);
                completed.Should().NotBeNull();
                completed!.Status.Should().Be(InteractionStatus.Completed);
                completed.CompletedDate.Should().NotBeNull();
                completed.ResolutionNotes.Should().Be("Müşteri ile görüşüldü, teklif onaylandı.");
            }

            // Yönetici gündeminden de tamamen çıkmış olmalı
            var agenda = await readService.GetManagerAgendaAsync();
            agenda.Value.Should().NotBeNull();
            agenda.Value!.Should().NotContain(x => x.Id == interaction.Id);
        }

        /// <summary>
        /// TEST 4 (Interaction Validation & Event):
        /// Yeni etkileşim eklerken zorunlu alan doğrulama kurallarının çalışması ve
        /// başarılı eklemede EventAggregator üzerinden CustomerInteractionChangedEvent olayının tetiklenmesi.
        /// </summary>
        [Fact]
        public async Task Test4_InteractionValidation_And_EventAggregatorPublish_WorksReliably()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            var (_, _, readService, commandService) = CreateServices(dbName);

            var toastMock = new Mock<IToastService>();
            var loadingMock = new Mock<ILoadingService>();
            var userAppMock = new Mock<IUserAppService>();
            userAppMock.Setup(u => u.GetAllAsync(default))
                .ReturnsAsync(Result.Success(new List<UserListItemDto>()));

            var vm = new QuickInteractionAddViewModel(
                commandService,
                readService,
                userAppMock.Object,
                toastMock.Object,
                loadingMock.Object);

            // 1. Doğrulama: Zorunlu alanlar boşken CanSave false olmalı
            vm.CallerName = "";
            vm.CallerPhone = "";
            vm.Subject = "";
            vm.Summary = "";
            vm.SaveCommand.CanExecute(null).Should().BeFalse("Zorunlu alanlar boşken kaydetmeye izin verilmemelidir.");

            // Kısmi doldurma
            vm.CallerName = "Serkan Yılmaz";
            vm.SaveCommand.CanExecute(null).Should().BeFalse();

            vm.CallerPhone = "05329998877";
            vm.SaveCommand.CanExecute(null).Should().BeFalse();

            vm.Subject = "Yangın Pompası Arızası";
            vm.SaveCommand.CanExecute(null).Should().BeFalse();

            // Tüm zorunlu alanlar doldurulduğunda CanSave true olmalı
            vm.Summary = "Bodrum kattaki yangın pompası basınç düşürmüş.";
            vm.SaveCommand.CanExecute(null).Should().BeTrue("Tüm zorunlu alanlar doldurulduğunda CanSave true olmalıdır.");

            // 2. EventAggregator Olay Yayını Doğrulaması
            var receivedEvents = new List<CustomerInteractionChangedEvent>();
            using var sub = EventAggregator.Instance.Subscribe<CustomerInteractionChangedEvent>(e =>
            {
                receivedEvents.Add(e);
            });

            // Act: Formu kaydet
            await vm.SaveCommand.ExecuteAsync(null);

            // Assert
            toastMock.Verify(t => t.ShowSuccess(It.IsAny<string>(), It.IsAny<string>()), Times.Once,
                "Başarılı kayıt sonrası kullanıcıya toast mesajı gösterilmelidir.");

            receivedEvents.Should().NotBeEmpty("Kayıt eklendiğinde EventAggregator üzerinden CustomerInteractionChangedEvent yayınlanmalıdır.");
            var publishedEvent = receivedEvents.Last();
            publishedEvent.ChangeType.Should().Be(InteractionChangeType.Created);
            publishedEvent.InteractionId.Should().BeGreaterThan(0);
            publishedEvent.InteractionNumber.Should().StartWith("TALEP-");
        }
    }
}
