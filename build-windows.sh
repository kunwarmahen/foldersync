#!/bin/bash

###############################################################################
#                                                                             #
#  SyncManager Windows Build Script for Ubuntu Linux                         #
#                                                                             #
#  Builds Windows .exe files from C# source code on Ubuntu                   #
#  Supports: Windows x64, x86, ARM64                                         #
#                                                                             #
#  Usage:
#    chmod +x build-windows.sh
#    ./build-windows.sh
#                                                                             #
###############################################################################

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_NAME="SyncManager"
PROJECT_DIR="${PROJECT_DIR:-$HOME/projects/SyncManager/$PROJECT_NAME}"
RELEASE_DIR="$PROJECT_DIR/bin/Release/net8.0-windows"
OUTPUT_DIR="${OUTPUT_DIR:-$HOME/SyncManager-releases}"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

# Arrays
declare -a ARCHITECTURES=("win-x64" "win-x86" "win-arm64")
declare -a ARCH_NAMES=("64-bit Windows" "32-bit Windows" "ARM64 Windows")

###############################################################################
# Functions
###############################################################################

print_header() {
    echo ""
    echo -e "${BLUE}╔════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║${NC}  $1"
    echo -e "${BLUE}╚════════════════════════════════════════════════════════╝${NC}"
    echo ""
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

check_dotnet() {
    if ! command -v dotnet &> /dev/null; then
        print_error ".NET 8 SDK not found!"
        echo ""
        echo "Install .NET 8 on Ubuntu:"
        echo "  wget https://dot.net/v1/dotnet-install.sh"
        echo "  bash dotnet-install.sh --channel 8.0"
        echo "  export PATH=\$HOME/.dotnet:\$PATH"
        echo ""
        exit 1
    fi
    
    VERSION=$(dotnet --version)
    if [[ ! $VERSION =~ ^8\. ]]; then
        print_warning ".NET version is $VERSION, but .NET 8.0+ is required"
    fi
    
    print_success ".NET SDK: $VERSION"
}

check_project() {
    if [ ! -f "$PROJECT_DIR/SyncManager.csproj" ]; then
        print_error "Project file not found: $PROJECT_DIR/SyncManager.csproj"
        echo ""
        echo "Expected structure:"
        echo "  $PROJECT_DIR/"
        echo "  ├── SyncManager.csproj"
        echo "  ├── MainWindow.xaml"
        echo "  ├── MainWindow.xaml.cs"
        echo "  ├── Models.cs"
        echo "  ├── Services.cs"
        echo "  ├── App.xaml"
        echo "  └── App.xaml.cs"
        echo ""
        exit 1
    fi
    
    print_success "Project file found: SyncManager.csproj"
}

check_source_files() {
    local required_files=("SyncManager.xaml" "MainWindow.xaml.cs" "Models.cs" "Services.cs" "App.xaml" "App.xaml.cs")
    local missing_files=()
    
    cd "$PROJECT_DIR"
    
    for file in "${required_files[@]}"; do
        if [ ! -f "$file" ]; then
            missing_files+=("$file")
        fi
    done
    
    if [ ${#missing_files[@]} -gt 0 ]; then
        print_error "Missing source files:"
        for file in "${missing_files[@]}"; do
            echo "  - $file"
        done
        exit 1
    fi
    
    print_success "All source files present"
}

restore_dependencies() {
    print_info "Restoring dependencies..."
    cd "$PROJECT_DIR"
    
    if dotnet restore > /tmp/dotnet-restore.log 2>&1; then
        print_success "Dependencies restored"
    else
        print_error "Failed to restore dependencies"
        cat /tmp/dotnet-restore.log
        exit 1
    fi
}

clean_build() {
    print_info "Cleaning previous build..."
    cd "$PROJECT_DIR"
    
    if [ -d "bin/Release" ]; then
        rm -rf bin/Release
        print_success "Cleaned build directory"
    fi
}

build_architecture() {
    local arch=$1
    local arch_name=$2
    
    echo ""
    print_info "Building for $arch_name ($arch)..."
    
    cd "$PROJECT_DIR"
    
    if dotnet publish -c Release -r "$arch" --self-contained > /tmp/dotnet-build-$arch.log 2>&1; then
        local exe_path="$RELEASE_DIR/$arch/publish/SyncManager.exe"
        local exe_size=$(ls -lh "$exe_path" | awk '{print $5}')
        
        print_success "$arch build complete ($exe_size)"
        echo "$exe_path"
        return 0
    else
        print_error "$arch build failed"
        cat /tmp/dotnet-build-$arch.log
        return 1
    fi
}

build_all_architectures() {
    print_header "Building for Windows Architectures"
    
    local failed=0
    local -a exe_paths
    
    for i in "${!ARCHITECTURES[@]}"; do
        if ! build_architecture "${ARCHITECTURES[$i]}" "${ARCH_NAMES[$i]}"; then
            failed=$((failed + 1))
        else
            exe_paths+=("$(echo)")
        fi
    done
    
    if [ $failed -gt 0 ]; then
        print_error "$failed architecture(s) failed to build"
        return 1
    fi
    
    print_success "All architectures built successfully"
    return 0
}

create_release_packages() {
    print_header "Creating Release Packages"
    
    mkdir -p "$OUTPUT_DIR"
    
    # Create individual architecture folders
    for arch in "${ARCHITECTURES[@]}"; do
        local arch_dir="$OUTPUT_DIR/$arch"
        mkdir -p "$arch_dir"
        
        local exe_src="$RELEASE_DIR/$arch/publish/SyncManager.exe"
        if [ -f "$exe_src" ]; then
            cp "$exe_src" "$arch_dir/"
            print_success "Copied $arch/SyncManager.exe"
        fi
    done
    
    # Create README
    cat > "$OUTPUT_DIR/README.txt" << 'EOF'
═══════════════════════════════════════════════════════════════════════════════
                        SYNC MANAGER - Windows Executable
═══════════════════════════════════════════════════════════════════════════════

This package contains compiled SyncManager.exe for Windows.

CHOOSE YOUR ARCHITECTURE:

  win-x64/   → 64-bit Windows (Most Common) ← Choose this for most systems
  win-x86/   → 32-bit Windows (Older systems)
  win-arm64/ → ARM-based Windows (Surface Pro X, etc.)

HOW TO USE:

  1. Choose the appropriate folder for your Windows version
  2. Copy SyncManager.exe to your Windows machine
  3. Run SyncManager.exe directly (no installation needed!)
  4. Create sync profiles and start syncing

SYSTEM REQUIREMENTS:

  • Windows 10 or later
  • No additional software required
  • All dependencies included in the .exe (self-contained build)

FEATURES:

  ✓ Folder monitoring and sync
  ✓ Automatic backup versioning
  ✓ Professional GUI with 5 tabs
  ✓ System tray integration
  ✓ Activity logging
  ✓ Windows Task Scheduler support

BUILDING:

  Built on Ubuntu with .NET 8 SDK
  
  Build date: $(date)
  .NET version: $(dotnet --version)
  
SUPPORT:

  See included documentation files for detailed usage instructions

═══════════════════════════════════════════════════════════════════════════════
EOF
    
    print_success "Created README.txt"
    
    # Create SHA256 checksums
    cd "$OUTPUT_DIR"
    for arch in "${ARCHITECTURES[@]}"; do
        if [ -f "$arch/SyncManager.exe" ]; then
            sha256sum "$arch/SyncManager.exe" > "$arch/SHA256.txt"
            print_success "Created checksum for $arch"
        fi
    done
    
    # Create archive
    print_info "Creating compressed archives..."
    
    if command -v tar &> /dev/null; then
        tar -czf "SyncManager-$TIMESTAMP.tar.gz" -C "$OUTPUT_DIR/.." "$(basename $OUTPUT_DIR)"
        print_success "Created: SyncManager-$TIMESTAMP.tar.gz"
    fi
    
    if command -v zip &> /dev/null; then
        cd "$OUTPUT_DIR"
        zip -r "SyncManager-$TIMESTAMP.zip" . -q
        print_success "Created: SyncManager-$TIMESTAMP.zip"
        cd - > /dev/null
    fi
}

show_final_info() {
    print_header "Build Complete!"
    
    echo "Release files location:"
    echo "  $OUTPUT_DIR"
    echo ""
    
    echo "Available builds:"
    for i in "${!ARCHITECTURES[@]}"; do
        local arch="${ARCHITECTURES[$i]}"
        local arch_name="${ARCH_NAMES[$i]}"
        local exe="$OUTPUT_DIR/$arch/SyncManager.exe"
        
        if [ -f "$exe" ]; then
            local size=$(ls -lh "$exe" | awk '{print $5}')
            echo "  ✓ $arch_name: $size"
        fi
    done
    
    echo ""
    echo "Next steps:"
    echo "  1. Transfer SyncManager.exe to your Windows machine"
    echo "  2. Run SyncManager.exe"
    echo "  3. Follow the GUI to create sync profiles"
    echo ""
    
    echo "Direct paths:"
    for arch in "${ARCHITECTURES[@]}"; do
        echo "  $OUTPUT_DIR/$arch/SyncManager.exe"
    done
    
    echo ""
}

show_usage() {
    cat << 'EOF'
SyncManager Windows Build Script for Ubuntu

USAGE:
  ./build-windows.sh [options]

OPTIONS:
  -h, --help              Show this help message
  -p, --project DIR       Set project directory (default: ~/projects/SyncManager/SyncManager)
  -o, --output DIR        Set output directory (default: ~/SyncManager-releases)
  -a, --arch ARCH         Build only specific architecture (win-x64, win-x86, win-arm64)
  -c, --clean             Clean before building
  -v, --version           Show version info

EXAMPLES:
  # Build all architectures
  ./build-windows.sh

  # Build only 64-bit Windows
  ./build-windows.sh --arch win-x64

  # Custom paths
  ./build-windows.sh -p /home/user/projects/SyncManager -o /tmp/output

EOF
}

###############################################################################
# Main
###############################################################################

main() {
    print_header "SyncManager Windows Build on Ubuntu"
    
    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            -h|--help) show_usage; exit 0 ;;
            -p|--project) PROJECT_DIR="$2"; shift 2 ;;
            -o|--output) OUTPUT_DIR="$2"; shift 2 ;;
            -c|--clean) CLEAN=true; shift ;;
            -v|--version) dotnet --version; exit 0 ;;
            *) echo "Unknown option: $1"; show_usage; exit 1 ;;
        esac
    done
    
    # Check prerequisites
    print_header "Checking Prerequisites"
    check_dotnet
    check_project
    check_source_files
    
    # Build
    restore_dependencies
    
    if [ "$CLEAN" = true ]; then
        clean_build
    fi
    
    if ! build_all_architectures; then
        print_error "Build failed!"
        exit 1
    fi
    
    # Package
    create_release_packages
    
    # Show results
    show_final_info
    
    print_success "Ready for Windows deployment!"
}

# Run main function
main "$@"
