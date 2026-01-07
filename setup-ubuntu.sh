#!/bin/bash

###############################################################################
#                                                                             #
#  SyncManager Setup Script for Ubuntu                                       #
#                                                                             #
#  Installs .NET 8 SDK and prepares project for building Windows .exe        #
#                                                                             #
#  Usage:
#    chmod +x setup-ubuntu.sh
#    ./setup-ubuntu.sh
#                                                                             #
###############################################################################

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

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

print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

# Check OS
check_os() {
    if [[ "$OSTYPE" != "linux-gnu"* ]]; then
        print_error "This script is for Linux only"
        echo "Detected OS: $OSTYPE"
        exit 1
    fi
    
    if ! grep -qi ubuntu /etc/os-release 2>/dev/null && ! grep -qi debian /etc/os-release 2>/dev/null; then
        print_warning "This script is optimized for Ubuntu/Debian"
        echo "Other Linux distributions may work but are not tested"
    fi
    
    print_success "Running on Linux"
}

# Check if .NET is already installed
check_existing_dotnet() {
    if command -v dotnet &> /dev/null; then
        local version=$(dotnet --version)
        print_info ".NET is already installed: $version"
        
        if [[ $version =~ ^8\. ]]; then
            print_success ".NET 8 is installed - skipping installation"
            return 0
        else
            print_warning ".NET 8 is required, but you have $version"
            print_info "Proceeding with installation anyway..."
            return 1
        fi
    fi
    return 1
}

# Install .NET 8 using official script
install_dotnet() {
    print_header "Installing .NET 8 SDK"
    
    local install_dir="$HOME/.dotnet"
    
    if [ -d "$install_dir" ]; then
        print_info "Found existing .NET installation at $install_dir"
        print_warning "Backing up to $install_dir.bak"
        mv "$install_dir" "$install_dir.bak"
    fi
    
    print_info "Downloading .NET 8 installer..."
    
    if ! wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh; then
        print_error "Failed to download .NET installer"
        echo "Try manual installation:"
        echo "  https://learn.microsoft.com/en-us/dotnet/core/install/linux"
        exit 1
    fi
    
    chmod +x /tmp/dotnet-install.sh
    
    print_info "Installing .NET 8 (this may take a few minutes)..."
    
    if /tmp/dotnet-install.sh --channel 8.0 --install-dir "$install_dir"; then
        print_success ".NET 8 installed to $install_dir"
    else
        print_error "Installation failed"
        exit 1
    fi
    
    rm -f /tmp/dotnet-install.sh
}

# Setup PATH
setup_path() {
    print_header "Setting up PATH"
    
    local dotnet_path="$HOME/.dotnet"
    
    if [[ ":$PATH:" == *":$dotnet_path:"* ]]; then
        print_success "PATH already configured"
        return 0
    fi
    
    print_info "Adding .NET to PATH..."
    
    export PATH="$dotnet_path:$PATH"
    
    # Add to bashrc
    if [ -f "$HOME/.bashrc" ]; then
        if ! grep -q "export PATH=.*\.dotnet" "$HOME/.bashrc"; then
            echo 'export PATH="$HOME/.dotnet:$PATH"' >> "$HOME/.bashrc"
            print_success "Added to ~/.bashrc"
        fi
    fi
    
    # Add to zshrc if it exists
    if [ -f "$HOME/.zshrc" ]; then
        if ! grep -q "export PATH=.*\.dotnet" "$HOME/.zshrc"; then
            echo 'export PATH="$HOME/.dotnet:$PATH"' >> "$HOME/.zshrc"
            print_success "Added to ~/.zshrc"
        fi
    fi
    
    # Reload shell config
    if [ -f "$HOME/.bashrc" ]; then
        source "$HOME/.bashrc"
    fi
}

# Verify installation
verify_dotnet() {
    print_header "Verifying Installation"
    
    if ! command -v dotnet &> /dev/null; then
        print_error ".NET command not found after installation"
        print_info "Try reloading your shell:"
        echo "  source ~/.bashrc"
        exit 1
    fi
    
    local version=$(dotnet --version)
    print_success ".NET version: $version"
    
    if [[ ! $version =~ ^8\. ]]; then
        print_warning "Expected .NET 8.x but got $version"
        print_warning "Some features may not work correctly"
    fi
    
    # Check for Windows Desktop SDK
    print_info "Checking for Windows Desktop support..."
    
    if dotnet workload list 2>/dev/null | grep -q "wsl"; then
        print_success "Windows Desktop workload available"
    else
        print_warning "Windows Desktop workload may need to be installed"
        print_info "Run: dotnet workload install desktop"
    fi
}

# Setup project directory
setup_project() {
    print_header "Setting Up Project Directory"
    
    local project_base="$HOME/projects/SyncManager"
    local project_dir="$project_base/SyncManager"
    
    mkdir -p "$project_dir"
    print_success "Created project directory: $project_dir"
    
    print_info "Downloading SyncManager source files..."
    
    # Create placeholder message
    cat > "$project_dir/SETUP.md" << 'EOF'
# SyncManager Setup for Ubuntu

Your project directory is set up at: ~/.../SyncManager

## Next Steps:

1. Copy the C# source files into this directory:
   - SyncManager.xaml
   - MainWindow.xaml.cs
   - Models.cs
   - Services.cs
   - App.xaml
   - App.xaml.cs
   - SyncManager.csproj

2. Copy to this location:
   cp ~/Downloads/SyncManager.* ~/projects/SyncManager/SyncManager/

3. Build for Windows:
   cd ~/projects/SyncManager/SyncManager
   dotnet publish -c Release -r win-x64 --self-contained

4. Find your .exe in:
   ~/projects/SyncManager/SyncManager/bin/Release/net8.0-windows/win-x64/publish/SyncManager.exe

EOF
    
    print_success "Created SETUP.md in project directory"
}

# Install build tools (optional)
install_optional_tools() {
    print_header "Optional Build Tools"
    
    print_info "Install optional tools? (git, nano, etc.) [y/N]"
    read -r response
    
    if [[ "$response" =~ ^[Yy]$ ]]; then
        print_info "Installing optional tools..."
        
        sudo apt update > /dev/null 2>&1
        
        if command -v git &> /dev/null; then
            print_success "git already installed"
        else
            sudo apt install -y git > /dev/null 2>&1
            print_success "Installed git"
        fi
        
        if command -v nano &> /dev/null; then
            print_success "nano already installed"
        else
            sudo apt install -y nano > /dev/null 2>&1
            print_success "Installed nano"
        fi
        
        if command -v wget &> /dev/null; then
            print_success "wget already installed"
        else
            sudo apt install -y wget > /dev/null 2>&1
            print_success "Installed wget"
        fi
    fi
}

# Setup build scripts
setup_build_scripts() {
    print_header "Setting Up Build Scripts"
    
    local script_dir="$HOME/projects/SyncManager"
    
    # Check if build script exists
    if [ -f "$(pwd)/build-windows.sh" ]; then
        cp "$(pwd)/build-windows.sh" "$script_dir/"
        chmod +x "$script_dir/build-windows.sh"
        print_success "Copied build-windows.sh to $script_dir"
    else
        print_warning "build-windows.sh not found in current directory"
    fi
}

# Show final instructions
show_final_instructions() {
    print_header "Setup Complete!"
    
    echo "You can now build Windows applications on Ubuntu!"
    echo ""
    echo "Quick Start:"
    echo ""
    echo "  1. Copy SyncManager source files:"
    echo "     cp ~/Downloads/SyncManager.* ~/projects/SyncManager/SyncManager/"
    echo ""
    echo "  2. Navigate to project:"
    echo "     cd ~/projects/SyncManager/SyncManager"
    echo ""
    echo "  3. Build for Windows:"
    echo "     dotnet publish -c Release -r win-x64 --self-contained"
    echo ""
    echo "  4. Or use the build script:"
    echo "     ~/projects/SyncManager/build-windows.sh"
    echo ""
    echo "Output:"
    echo "  SyncManager.exe → ~/projects/SyncManager/SyncManager/bin/Release/net8.0-windows/win-x64/publish/"
    echo ""
    echo "Verify Installation:"
    echo "  dotnet --version"
    echo ""
}

# Main
main() {
    print_header "SyncManager Setup for Ubuntu"
    
    check_os
    
    if check_existing_dotnet; then
        print_success ".NET 8 already installed - skipping installation"
    else
        install_dotnet
    fi
    
    setup_path
    
    # Reload PATH for verification
    export PATH="$HOME/.dotnet:$PATH"
    
    verify_dotnet
    setup_project
    install_optional_tools
    setup_build_scripts
    show_final_instructions
    
    print_success "Setup complete! Happy building! 🚀"
}

main "$@"
