<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:FinDesk.ViewModels"
        xmlns:lvc="clr-namespace:LiveChartsCore.SkiaSharpView.Avalonia;assembly=LiveChartsCore.SkiaSharpView.Avalonia"
        x:Class="FinDesk.MainWindow"
        Width="1100" Height="720"
        MinWidth="980" MinHeight="640"
        Title="FinDesk — Personal Finance">

  <Window.Resources>
    <DataTemplate DataType="{x:Type vm:DashboardViewModel}">
      <ScrollViewer>
        <StackPanel Margin="18" Spacing="14">
          <TextBlock Text="Дашборд" FontSize="20" FontWeight="SemiBold"/>

          <StackPanel Orientation="Horizontal" Spacing="12">
            <Border Background="#111827" CornerRadius="12" Padding="14" Width="240">
              <StackPanel>
                <TextBlock Text="Доходи" Foreground="#9CA3AF"/>
                <TextBlock Text="{Binding Income}" FontSize="22" Foreground="White"/>
              </StackPanel>
            </Border>
            <Border Background="#111827" CornerRadius="12" Padding="14" Width="240">
              <StackPanel>
                <TextBlock Text="Витрати" Foreground="#9CA3AF"/>
                <TextBlock Text="{Binding Expense}" FontSize="22" Foreground="White"/>
              </StackPanel>
            </Border>
            <Border Background="#111827" CornerRadius="12" Padding="14" Width="240">
              <StackPanel>
                <TextBlock Text="Баланс" Foreground="#9CA3AF"/>
                <TextBlock Text="{Binding Net}" FontSize="22" Foreground="White"/>
              </StackPanel>
            </Border>
          </StackPanel>

          <Grid ColumnDefinitions="*,*" RowDefinitions="Auto,Auto" ColumnSpacing="14" RowSpacing="14">
            <Border Grid.Row="0" Grid.Column="0" Background="#0B1220" CornerRadius="12" Padding="12">
              <StackPanel>
                <TextBlock Text="Витрати по категоріях (топ 8)" Foreground="#9CA3AF" Margin="0,0,0,6"/>
                <lvc:PieChart Series="{Binding PieSeries}" LegendPosition="Right" />
              </StackPanel>
            </Border>

            <Border Grid.Row="0" Grid.Column="1" Background="#0B1220" CornerRadius="12" Padding="12">
              <StackPanel>
                <TextBlock Text="Витрати по днях" Foreground="#9CA3AF" Margin="0,0,0,6"/>
                <lvc:CartesianChart Series="{Binding LineSeries}" XAxes="{Binding XAxes}" YAxes="{Binding YAxes}" />
              </StackPanel>
            </Border>
          </Grid>
        </StackPanel>
      </ScrollViewer>
    </DataTemplate>

    <DataTemplate DataType="{x:Type vm:ImportViewModel}">
      <StackPanel Margin="18" Spacing="12">
        <TextBlock Text="Імпорт" FontSize="20" FontWeight="SemiBold"/>

        <Border Background="#0B1220" CornerRadius="12" Padding="18"
                AllowDrop="True"
                DragDrop.DragOver="OnDragOver"
                DragDrop.Drop="OnDrop">
          <StackPanel Spacing="8">
            <TextBlock Text="{Binding HintText}" Foreground="#D1D5DB"/>
            <TextBlock Text="Порада: експортуй виписку з банку в CSV або XLSX і просто перетягни файл сюди." Foreground="#9CA3AF"/>
          </StackPanel>
        </Border>

        <Border Background="#111827" CornerRadius="12" Padding="12">
          <StackPanel Spacing="8">
            <TextBlock Text="Історія імпорту" Foreground="#9CA3AF"/>
            <ItemsControl ItemsSource="{Binding ImportedFiles}" />
          </StackPanel>
        </Border>
      </StackPanel>
    </DataTemplate>

    <DataTemplate DataType="{x:Type vm:SourcesViewModel}">
      <ScrollViewer>
        <StackPanel Margin="18" Spacing="12">
          <TextBlock Text="Банки / API" FontSize="20" FontWeight="SemiBold"/>

          <Border Background="#0B1220" CornerRadius="12" Padding="14">
            <StackPanel Spacing="10">
              <TextBlock Text="Monobank" Foreground="#9CA3AF"/>
              <TextBlock Text="Встав X-Token і натисни «Синхронізувати»." Foreground="#D1D5DB"/>
              <TextBox Watermark="X-Token" Text="{Binding MonobankToken}" PasswordChar="●"/>
              <StackPanel Orientation="Horizontal" Spacing="10">
                <Button Content="Зберегти" Command="{Binding SaveCommand}" />
                <Button Content="Синхронізувати Monobank" Command="{Binding SyncMonobankCommand}" />
              </StackPanel>
            </StackPanel>
          </Border>

          <Border Background="#0B1220" CornerRadius="12" Padding="14">
            <StackPanel Spacing="10">
              <TextBlock Text="PrivatBank (Автоклієнт)" Foreground="#9CA3AF"/>
              <TextBox Watermark="Base URL (опційно)" Text="{Binding PrivatBaseUrl}"/>
              <TextBox Watermark="ClientId (опційно)" Text="{Binding PrivatClientId}"/>
              <TextBox Watermark="Token" Text="{Binding PrivatToken}" PasswordChar="●"/>
              <StackPanel Orientation="Horizontal" Spacing="10">
                <Button Content="Зберегти" Command="{Binding SaveCommand}" />
                <Button Content="Тест API" Command="{Binding TestPrivatCommand}" />
              </StackPanel>
              <TextBlock Text="Якщо API не працює — імпорт CSV/XLSX залишається основним сценарієм." Foreground="#9CA3AF"/>
            </StackPanel>
          </Border>

          <Border Background="#0B1220" CornerRadius="12" Padding="14">
            <StackPanel Spacing="10">
              <TextBlock Text="UKRSIB (Open Banking)" Foreground="#9CA3AF"/>
              <TextBox Watermark="Шлях до сертифіката (QWAC, Advanced)" Text="{Binding UkrsibCertificatePath}"/>
              <TextBlock Text="{Binding InfoText}" Foreground="#9CA3AF"/>
              <Button Content="Зберегти" Command="{Binding SaveCommand}" Width="140"/>
            </StackPanel>
          </Border>

          <Border Background="#111827" CornerRadius="12" Padding="12">
            <TextBlock Text="{Binding InfoText}" Foreground="#D1D5DB"/>
          </Border>
        </StackPanel>
      </ScrollViewer>
    </DataTemplate>

    <DataTemplate DataType="{x:Type vm:TransactionsViewModel}">
      <StackPanel Margin="18" Spacing="12">
        <TextBlock Text="Транзакції" FontSize="20" FontWeight="SemiBold"/>

        <DataGrid ItemsSource="{Binding Items}" AutoGenerateColumns="False" IsReadOnly="False" GridLinesVisibility="None">
          <DataGrid.Columns>
            <DataGridTextColumn Header="Дата" Binding="{Binding DateUtc}" Width="180"/>
            <DataGridTextColumn Header="Джерело" Binding="{Binding Source}" Width="110"/>
            <DataGridTextColumn Header="Опис" Binding="{Binding Description}" Width="*"/>
            <DataGridTextColumn Header="Merchant" Binding="{Binding Merchant}" Width="220"/>
            <DataGridTextColumn Header="Сума" Binding="{Binding Amount}" Width="110"/>

            <DataGridTemplateColumn Header="Категорія" Width="170">
              <DataGridTemplateColumn.CellTemplate>
                <DataTemplate>
                  <ComboBox ItemsSource="{Binding DataContext.Categories, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                            SelectedItem="{Binding Category}"
                            SelectionChanged="OnCategoryChanged"/>
                </DataTemplate>
              </DataGridTemplateColumn.CellTemplate>
            </DataGridTemplateColumn>
          </DataGrid.Columns>
        </DataGrid>
      </StackPanel>
    </DataTemplate>
  </Window.Resources>

  <Grid RowDefinitions="Auto,*" ColumnDefinitions="260,*">
    <Border Grid.Row="0" Grid.ColumnSpan="2" Background="#111827" Padding="16">
      <DockPanel>
        <TextBlock DockPanel.Dock="Left" Text="FinDesk" Foreground="White" FontSize="18" FontWeight="SemiBold"/>
        <TextBlock DockPanel.Dock="Right" Text="{Binding StatusText}" Foreground="#D1D5DB" VerticalAlignment="Center"/>
      </DockPanel>
    </Border>

    <Border Grid.Row="1" Grid.Column="0" Background="#0B1220" Padding="12">
      <StackPanel Spacing="10">
        <TextBlock Text="Навігація" Foreground="#9CA3AF" FontSize="12"/>
        <Button Content="Дашборд" Command="{Binding GoDashboard}" />
        <Button Content="Транзакції" Command="{Binding GoTransactions}" />
        <Button Content="Імпорт" Command="{Binding GoImport}" />
        <Button Content="Банки / API" Command="{Binding GoSources}" />

        <Border Height="1" Background="#1F2937" Margin="0,8,0,8"/>

        <TextBlock Text="Період" Foreground="#9CA3AF" FontSize="12"/>
        <ComboBox ItemsSource="{Binding PeriodPresets}" SelectedItem="{Binding SelectedPreset}" DisplayMemberBinding="{Binding Display}" />
        <StackPanel Orientation="Horizontal" Spacing="8">
          <DatePicker SelectedDate="{Binding FromDate}" Width="115"/>
          <DatePicker SelectedDate="{Binding ToDate}" Width="115"/>
        </StackPanel>
        <Button Content="Оновити дані" Command="{Binding RefreshAll}" />
      </StackPanel>
    </Border>

    <ContentControl Grid.Row="1" Grid.Column="1" Content="{Binding CurrentPage}" />
  </Grid>
</Window>
