#!/bin/bash
set -e

VERSION="1.0"
PACKAGE_NAME="passflow-tracker"      
APP_EXECUTABLE="PassFlow Tracker"     

BUILD_DIR="${PACKAGE_NAME}_${VERSION}"

echo "=== 0. Восстановление NuGet-пакетов ==="
dotnet restore

dotnet clean -c Release
rm -rf bin/ obj/ publish/

echo "=== 1. Публикация приложения ==="
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./publish

echo "=== 2. Создание структуры пакета ==="
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR/DEBIAN"
mkdir -p "$BUILD_DIR/usr/bin"
mkdir -p "$BUILD_DIR/usr/share/applications"
mkdir -p "$BUILD_DIR/usr/share/icons/hicolor/256x256/apps"
mkdir -p "$BUILD_DIR/opt/$PACKAGE_NAME"

echo "=== 3. Копирование файлов ==="
cp -r publish/* "$BUILD_DIR/opt/$PACKAGE_NAME/"

cp Assets/icon.png "$BUILD_DIR/usr/share/icons/hicolor/256x256/apps/passflow.png" 2>/dev/null || echo "Иконка не найдена"

echo "=== 4. Создание control ==="
cat > "$BUILD_DIR/DEBIAN/control" << EOF
Package: ${PACKAGE_NAME}
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: amd64
Maintainer: Ivan <shkantov.i@gmail.com>
Depends: docker.io, xdg-desktop-portal, xdg-desktop-portal-gtk | xdg-desktop-portal-kde, libc6 (>= 2.31)
Description: Система анализа пассажиропотока
 Приложение для анализа данных пассажирских перевозок.
EOF

echo "=== 5. Создание postinst ==="
cat > "$BUILD_DIR/DEBIAN/postinst" << 'EOF'
#!/bin/bash
set -e
gtk-update-icon-cache /usr/share/icons/hicolor/ 2>/dev/null || true
update-desktop-database /usr/share/applications/ 2>/dev/null || true
echo "PassFlow Tracker установлен! Запуск: passflow-tracker"
EOF
chmod 755 "$BUILD_DIR/DEBIAN/postinst"

echo "=== 6. Создание postrm ==="
cat > "$BUILD_DIR/DEBIAN/postrm" << 'EOF'
#!/bin/bash
set -e
gtk-update-icon-cache /usr/share/icons/hicolor/ 2>/dev/null || true
update-desktop-database /usr/share/applications/ 2>/dev/null || true
EOF
chmod 755 "$BUILD_DIR/DEBIAN/postrm"

echo "=== 7. Создание launcher ==="
cat > "$BUILD_DIR/usr/bin/$PACKAGE_NAME" << EOF
#!/bin/bash
if ! command -v docker &> /dev/null; then
    zenity --error --text="Docker не установлен" 2>/dev/null || echo "Ошибка: Docker не установлен"
    exit 1
fi
exec "/opt/${PACKAGE_NAME}/${APP_EXECUTABLE}" "\$@"
EOF
chmod 755 "$BUILD_DIR/usr/bin/$PACKAGE_NAME"

echo "=== 8. Создание desktop-файла ==="
cat > "$BUILD_DIR/usr/share/applications/$PACKAGE_NAME.desktop" << EOF
[Desktop Entry]
Name=PassFlow Tracker
Comment=Анализ пассажиропотока
Exec=${PACKAGE_NAME}
Icon=passflow
Terminal=false
Type=Application
Categories=Utility;
StartupNotify=true
EOF

echo "=== 9. Права ==="
find "$BUILD_DIR" -type d -exec chmod 755 {} \;
find "$BUILD_DIR" -type f -exec chmod 644 {} \;
chmod 755 "$BUILD_DIR/DEBIAN/postinst"
chmod 755 "$BUILD_DIR/DEBIAN/postrm"
chmod 755 "$BUILD_DIR/usr/bin/$PACKAGE_NAME"
chmod 755 "$BUILD_DIR/opt/$PACKAGE_NAME/$APP_EXECUTABLE"

echo "=== 10. Проверка структуры ==="
echo "Файлы в /opt/${PACKAGE_NAME}/:"
ls -la "$BUILD_DIR/opt/$PACKAGE_NAME/"
echo ""
echo "Launcher:"
cat "$BUILD_DIR/usr/bin/$PACKAGE_NAME"
echo ""
echo "Структура пакета:"
find "$BUILD_DIR" -type f | sort

echo "=== 11. Сборка .deb ==="
dpkg-deb --build "$BUILD_DIR"

echo "=== Готово! ==="
echo "Файл: ${BUILD_DIR}.deb"
echo "Установка: sudo dpkg -i ${BUILD_DIR}.deb"
