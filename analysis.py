import numpy as np
import matplotlib.pyplot as plt
from io import StringIO
from scipy.ndimage import gaussian_filter1d

# Препроцессинг
file = "1.txt"

with open(file, 'r') as file:
    file_content = file.read()

file_content = file_content.replace(',', '.')
file_buffer = StringIO(file_content)

data = np.genfromtxt(file_buffer, delimiter='\t')

# Применям фильтр гаусса (делаем функцию более гладкой)
# Либо, если хотим исходную функцию, sigma = 1
sigma = 1
time = gaussian_filter1d((data[:, 0] * 1000), sigma)  # секунды -> милисекунды
voltage = gaussian_filter1d((data[:, 1] * 1000), sigma)  # вольты -> миливольты

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

#Потенциал покоя, обязательно ищем ручками, уникальный алгоритм придумывать пару лет будем
rmp = np.mean(voltage[(time > 0 ) & (time < 45 )])
print(f'Потенциал покоя: {rmp:.2f} мВ')

# Считаем время до 90% реполяризации (т.е. через сколько потенциал достигнет 90% от пика) и 50%
# На основе этого можно определить, пейсмейкерная клетка или нет
# Ес что, фича классическая, погуглите APD50 и APD90
def find_nearest(array, value):
    array = np.asarray(array)
    idx = (np.abs(array - value)).argmin()
    return idx

peak_voltage = np.max(voltage)
baseline_voltage = rmp
percentage_levels = [0.9, 0.5]

peak_idx = np.argmax(voltage)
for percentage in percentage_levels:
    target_voltage = baseline_voltage + (peak_voltage - baseline_voltage) * percentage
    depolarization_idx = find_nearest(voltage[:peak_idx], target_voltage)
    repolarization_idx = find_nearest(voltage[peak_idx:], target_voltage) + peak_idx
    APD = time[repolarization_idx] - time[depolarization_idx]
    print(f'Длительность ПД при {percentage*100:.0f}% реполяризации: {APD:.2f} мс')

# График ПД с APD50 и APD90
plt.plot(time, voltage)
plt.xlabel('Время (мс)')
plt.ylabel('Напряжение (мВ)')
plt.title('ПД')

peak_idx = np.argmax(voltage)
for percentage in percentage_levels:
    target_voltage = baseline_voltage + (peak_voltage - baseline_voltage) * percentage
    depolarization_idx = find_nearest(voltage[:peak_idx], target_voltage)
    repolarization_idx = find_nearest(voltage[peak_idx:], target_voltage) + peak_idx
    plt.annotate(f'{percentage*100:.0f}% Реполяризации',
                 xy=(time[repolarization_idx], voltage[repolarization_idx]),
                 xytext=(time[repolarization_idx], voltage[repolarization_idx] + 10),
                 arrowprops=dict(facecolor='black', arrowstyle='->'))
plt.show()

# Считаем Vmax деполяризации
voltage_derivative = np.gradient(voltage, time)
Vmax = np.max(voltage_derivative)
print(f'Максимальная скорость деполяризации: {Vmax:.2f} мВ/мс')

# График производной напряжения (скорость деполяризации)
plt.plot(time, voltage_derivative)
plt.xlabel('Время (мс)')
plt.ylabel('Производная напряжения (мВ/мс)')
plt.title('Скорость деполяризации')
plt.show()

