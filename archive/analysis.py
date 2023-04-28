import numpy as np
import matplotlib.pyplot as plt
from io import StringIO
from skimage.restoration import denoise_tv_chambolle
from scipy.optimize import least_squares 

# Препроцессинг
file = "1229.txt"
with open(file, 'r') as file:
    file_content = file.read()
file_content = file_content.replace(',', '.')
file_buffer = StringIO(file_content)

data = np.genfromtxt(file_buffer, delimiter='\t')

# Применим Total Variation Filtering (делаем функцию более гладкой)
# Меняем weight зависимости от требований
# Нужна более гладкая функци ? - Weight ставим больше
# Нужна менее сглаженная ? - Weight ставим поменьше
weight = 15
time = data[:, 0] * 1000  # секунды -> милисекунды
voltage = denoise_tv_chambolle((data[:, 1] * 1000), weight)

# График ПД
print(f'Пик ПД: {max(voltage):.2f} мВ')
max_idx = np.argmax(voltage)
plt.plot(time, voltage)
plt.scatter(time[max_idx], voltage[max_idx], color='green', marker='o')
plt.annotate('Maximum', xy=(time[max_idx], voltage[max_idx]), xytext=(time[max_idx], voltage[max_idx] - 0.0002))     
plt.xlabel('Время (мс)')
plt.ylabel('Напряжение (мВ)')
plt.title('ПД')
plt.show()

# Считаем производную и Vmax деполяризации
voltage_derivative = np.gradient(voltage, time)
Vmax = np.max(voltage_derivative)
print(f'Максимальная скорость деполяризации: {Vmax:.2f} мВ/мс')

# Строим график
plt.plot(time, voltage_derivative)
plt.xlabel('Время (мс)')
plt.ylabel('Производная напряжения (мВ/мс)')
plt.title('Скорость деполяризации')
plt.show()

# Ищем начало фазы 0
# Threshold - подбирается ручками, вроде это число достаточно точно отражает начало фазы 0
threshold = 1.8
phase_0_start_index = np.argmax(voltage_derivative > threshold)

#Строим график
plt.plot(time, voltage)
plt.xlabel('Время (мс)')
plt.ylabel('Напряжение (мВ)')
plt.title('ПД с началом фазы 0')

plt.axvline(time[phase_0_start_index], color='red', linestyle=':', label=f'Начало фазы 0 ({time[phase_0_start_index]:.5f} мс)')

num_points = 15
left_points = np.arange(phase_0_start_index - num_points, phase_0_start_index)
right_points = np.arange(phase_0_start_index + 1, phase_0_start_index + num_points + 1)

plt.scatter(time[left_points], voltage[left_points], color='blue', marker='o', label='Точки слева')
plt.scatter(time[right_points], voltage[right_points], color='green', marker='o', label='Точки справа')

plt.legend()
plt.show()

#Круг...
def circle_func(params, x, y):
    x0, y0, r = params
    return (x - x0) ** 2 + (y - y0) ** 2 - r ** 2

def fit_circle(x, y):
    def residuals(params):
        return circle_func(params, x, y)

    initial_guess = (np.mean(x), np.mean(y), np.std(y))
    res = least_squares(residuals, initial_guess)
    x0, y0, r = res.x
    return x0, y0, r

def plot_circle(ax, x0, y0, r):
    theta = np.linspace(0, 2 * np.pi, 100)
    x = x0 + r * np.cos(theta)
    y = y0 + r * np.sin(theta)
    ax.plot(x, y, 'r-', linewidth=1.5, label='Вписанный круг')

# Значения времени и напряжений, соответствующие примерному началу фазы 0
fit_time = time[left_points[0]:right_points[-1]+1]
fit_voltage = voltage[left_points[0]:right_points[-1]+1]

# Вписываем круг в данные
x0, y0, r = fit_circle(fit_time, fit_voltage)

#Строим график
fig, ax = plt.subplots()

ax.plot(time, voltage, label='ПД')
plot_circle(ax, x0, y0, r)
ax.scatter(fit_time, fit_voltage, color='blue', marker='o')

ax.set_xlabel('Время (мс)')
ax.set_ylabel('Напряжение (мВ)')
ax.set_title('Анализ фазы 0 с помощью радиуса круга')

ax.legend()
plt.show()

print(f"Радиус круга: {r:.2f}")