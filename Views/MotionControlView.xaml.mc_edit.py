import io

p = r'D:\wqz\code\NoCodeVision\Views\MotionControlView.xaml'
raw = io.open(p, encoding='utf-8', newline='').read()
t = raw.replace('\r\n', '\n')

reps = []

btn = [
    ('<Button Style="{StaticResource AppleBtn}" Content="新增" Command="{Binding AddCmd}" />',
     '<Button Style="{StaticResource AppleBtn}" Command="{Binding AddCmd}">\n'
     '    <StackPanel Orientation="Horizontal">\n'
     '        <TextBlock Text="\u2795" FontSize="13" Margin="0,0,6,0" VerticalAlignment="Center" />\n'
     '        <TextBlock Text="新增" VerticalAlignment="Center" />\n'
     '    </StackPanel>\n'
     '</Button>'),
    ('<Button Style="{StaticResource AppleBtnSecondary}" Content="删除" Command="{Binding DeleteCmd}" Margin="8,0,0,0" />',
     '<Button Style="{StaticResource AppleBtnSecondary}" Command="{Binding DeleteCmd}" Margin="8,0,0,0">\n'
     '    <StackPanel Orientation="Horizontal">\n'
     '        <TextBlock Text="\U0001F5D1" FontSize="13" Margin="0,0,6,0" VerticalAlignment="Center" />\n'
     '        <TextBlock Text="删除" VerticalAlignment="Center" />\n'
     '    </StackPanel>\n'
     '</Button>'),
    ('<Button Style="{StaticResource AppleBtnSecondary}" Content="重命名" Command="{Binding RenameCmd}" Margin="8,0,0,0" />',
     '<Button Style="{StaticResource AppleBtnSecondary}" Command="{Binding RenameCmd}" Margin="8,0,0,0">\n'
     '    <StackPanel Orientation="Horizontal">\n'
     '        <TextBlock Text="\u270f\ufe0f" FontSize="13" Margin="0,0,6,0" VerticalAlignment="Center" />\n'
     '        <TextBlock Text="重命名" VerticalAlignment="Center" />\n'
     '    </StackPanel>\n'
     '</Button>'),
    ('<Button Style="{StaticResource AppleBtn}" Content="新增点" Command="{Binding AddPointCmd}" />',
     '<Button Style="{StaticResource AppleBtn}" Command="{Binding AddPointCmd}">\n'
     '    <StackPanel Orientation="Horizontal">\n'
     '        <TextBlock Text="\u2795" FontSize="13" Margin="0,0,6,0" VerticalAlignment="Center" />\n'
     '        <TextBlock Text="新增点" VerticalAlignment="Center" />\n'
     '    </StackPanel>\n'
     '</Button>'),
    ('<Button Style="{StaticResource AppleBtnSecondary}" Content="删除点" Command="{Binding DeletePointCmd}" Margin="8,0,0,0" />',
     '<Button Style="{StaticResource AppleBtnSecondary}" Command="{Binding DeletePointCmd}" Margin="8,0,0,0">\n'
     '    <StackPanel Orientation="Horizontal">\n'
     '        <TextBlock Text="\U0001F5D1" FontSize="13" Margin="0,0,6,0" VerticalAlignment="Center" />\n'
     '        <TextBlock Text="删除点" VerticalAlignment="Center" />\n'
     '    </StackPanel>\n'
     '</Button>'),
    ('<Button Style="{StaticResource AppleBtnSecondary}" Content="重命名点" Command="{Binding RenamePointCmd}" Margin="8,0,0,0" />',
     '<Button Style="{StaticResource AppleBtnSecondary}" Command="{Binding RenamePointCmd}" Margin="8,0,0,0">\n'
     '    <StackPanel Orientation="Horizontal">\n'
     '        <TextBlock Text="\u270f\ufe0f" FontSize="13" Margin="0,0,6,0" VerticalAlignment="Center" />\n'
     '        <TextBlock Text="重命名点" VerticalAlignment="Center" />\n'
     '    </StackPanel>\n'
     '</Button>'),
]
for old, new in btn:
    c = t.count(old)
    assert c >= 1, 'button not found: ' + old[:40]
    t = t.replace(old, new)
    reps.append(('btn', c))

heads = [
    ('Text="轴参数"', 'Text="\U0001F3CA 轴参数"'),
    ('Text="IO 参数"', 'Text="\U0001F50C IO 参数"'),
    ('Text="气缸参数"', 'Text="\U0001F7E6 气缸参数"'),
    ('Text="点位参数"', 'Text="\U0001F4CD 点位参数"'),
    ('Text="料盘格参数"', 'Text="\U0001F532 料盘格参数"'),
]
for old, new in heads:
    c = t.count(old)
    assert c >= 1, 'header not found: ' + old
    t = t.replace(old, new)
    reps.append(('head', c))

old_col = '                                <GridViewColumn Header="名称" DisplayMemberBinding="{Binding Name}" Width="120" />'
new_col = ('                                <GridViewColumn Header="名称" Width="120">\n'
           '                                    <GridViewColumn.CellTemplate>\n'
           '                                        <DataTemplate>\n'
           '                                            <StackPanel Orientation="Horizontal" VerticalAlignment="Center">\n'
           '                                                <ctrl:AppleIconTile Glyph="\U0001F7E6" TileSize="20" TileBrush="{StaticResource iOSBlueBrush}" Margin="0,0,8,0" />\n'
           '                                                <TextBlock Text="{Binding Name}" VerticalAlignment="Center" />\n'
           '                                            </StackPanel>\n'
           '                                        </DataTemplate>\n'
           '                                    </GridViewColumn.CellTemplate>\n'
           '                                </GridViewColumn>')
assert t.count(old_col) == 1, t.count(old_col)
t = t.replace(old_col, new_col)
reps.append(('cyl-col', 1))

ns_old = '             xmlns:helpers="clr-namespace:NoCodeVision.Helpers"\n'
ns_new = ns_old + '             xmlns:ctrl="clr-namespace:NoCodeVision.Views.Controls"\n'
assert t.count(ns_old) == 1, t.count(ns_old)
t = t.replace(ns_old, ns_new)
reps.append(('ns', 1))

io.open(p, 'w', encoding='utf-8', newline='').write(t.replace('\n', '\r\n'))
print('replacements:', reps)
